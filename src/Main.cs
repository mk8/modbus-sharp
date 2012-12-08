using System;
using System.Threading;
using libmodbussharp;

namespace modbussharp
{
	class MainClass
	{

		static void Usage () {
			string usage = @"Modbus-sharp: a libmodbus-sharp testing application.

Server:
	modbus-sharp server listenAddress port [debug]

or
	modbus-sharp servermulti listenAddress port [debug]

Client - this configuration is used for testing protocol. Works on server, servermulti configuration as random-server-test from libmodbus package.
	modbus-sharp client serverAddress port

";
			Console.WriteLine (usage);
		}

		public static void Main(string[] args)
		{
			bool debug;

			if (args.Length < 1) {
				Usage ();
				System.Environment.Exit (1);
			}

			switch (args[0]) {
			case "server":
				if (args.Length < 3 || args.Length > 4) {
					Usage ();
					break;
				}
				debug = (args.Length == 3 ? false : true);
				ServerTest (args[1], Int32.Parse(args[2]),debug);
				break;

			case "servermulti":
				if (args.Length < 3 || args.Length > 4) {
					Usage ();
					break;
				}
				debug = (args.Length == 3 ? false : true);
				ServerTestMulti (args[1], Int32.Parse(args[2]),debug);
				break;

			case "client":
				if (args.Length < 3 || args.Length > 4) {
					Usage ();
					break;
				}
				debug = (args.Length == 3 ? false : true);
				ClientTest (args[1], Int32.Parse(args[2]),debug);
				break;

			default:

				Usage ();
				break;
			}


		}
		
		public static int ClientTest (string modbusAddress, int port, bool debug) {

			const int sizeMapping = 500;

			int LOOP = 100;
			// int SERVER_ID = 17;
			int ADDRESS_START = 0;
			int ADDRESS_END = 99;
		    int nb;
		    int nb_fail;
		    int nb_loop;
		    int addr;
			int rc;

			Random rnd=new Random();
			bool[] bitValues=new bool[sizeMapping];
			ushort[] shortValues=new ushort[sizeMapping];
	
			// Modbus Initialization 
			ModbusCore core = new ModbusCore(modbusAddress, port);
			core.Debug=debug;
			core.SetResponseTimeout (20000000);
			core.Connect();
		    if (core.MappingNew(sizeMapping,sizeMapping,sizeMapping,sizeMapping)) {
				Console.WriteLine("Failed to allocate the mapping.");
		        return -1;
		    }

		    nb_loop = nb_fail = 0;

			// Loop on all test cycle
		    while (nb_loop++ < LOOP) {
				Thread.Sleep (1);
				Console.WriteLine ("Step (" + nb_loop + ")");

				// Loop on all address
		        for (addr = ADDRESS_START; addr <= ADDRESS_END; ++addr) {
		            int i;
					
		            nb = ADDRESS_END - addr;

		            // Random numbers (short)
		            for (i=0; i<nb; ++i) {
						shortValues[i]=(ushort)rnd.Next(0xffff);
						core.RegisterRWUnsigned[i] = (ushort)shortValues[i];
						bitValues[i]=(bool)((shortValues[i] % 2) != 0);
						core.BitsRW[i] = bitValues[i];
		            }

		            // WRITE BIT
					bool oldBits = core.BitsRW [addr];
		            rc = core.BitsRWWrite(addr);
		            if (rc != 1) {
		                Console.WriteLine(string.Format("ERROR modbus_write_bit {0}", rc));
		                Console.WriteLine(string.Format("Address = {0}, value = {1}\n", addr, core.BitsRW[i]));
		                ++nb_fail;
		            } else {
		                core.BitsRWRead(addr);
		                if (rc != 1 || oldBits != core.BitsRW[addr] ) {
		                    Console.WriteLine(string.Format("ERROR modbus_read_bits single ({0})", rc));
		                    Console.WriteLine(string.Format("address = {0}", addr));
		                    ++nb_fail;
		                }
		            }
					
		            // MULTIPLE BITS
		            rc = core.BitsRWWrite(addr,nb);
		            if (rc != nb) {
		                Console.WriteLine(string.Format("ERROR modbus_write_bits ({0})", rc));
		                Console.WriteLine(string.Format("Address = {0}, nb = {1}", addr, nb));
		                ++nb_fail;
		            } else {
		                rc = core.BitsRWRead(addr, nb);
		                if (rc != nb) {
		                    Console.WriteLine("ERROR modbus_read_bits");
		                    Console.WriteLine(string.Format("Address = {0}, nb = {1}", addr, nb));
		                    ++nb_fail;
		                } else {
		                    for (i=0; i<nb; ++i) {
		                        if (core.BitsRW[i]  != bitValues[i]) {
		                            Console.WriteLine("ERROR modbus_read_bits");
		                            Console.WriteLine("Address = {0}, offset = {3}, value {1} (0x{1:X2}) != {2} (0x{2:X2})",
		                                   addr, core.BitsRW[i], bitValues[i],i);
		                            ++nb_fail;
		                        }
		                    }
		                }
		            }
					
		            // SINGLE REGISTER
		            rc = core.DirectWriteRegisterRW(addr, core.RegisterRWUnsigned[addr]);
		            if (rc != 1) {
						Console.WriteLine(string.Format("ERROR modbus_write_register ({0})", rc));
		                Console.WriteLine(string.Format("Address = {0}, value = {1} (0x{1:X2})",
		                       addr, core.RegisterRWSigned[0]));
		                ++nb_fail;
		            } else {
						core.RegisterRWUnsigned[addr] = 0;
		                rc = core.RegistersRWRead(addr);
		                if (rc != 1) {
		                    Console.WriteLine(string.Format("ERROR modbus_read_registers single ({0})", rc));
		                    Console.WriteLine(string.Format("Address = {0}", addr));
		                    ++nb_fail;
		                } else {
		                    if (core.RegisterRWUnsigned [addr] != shortValues[addr]) {
		                        Console.WriteLine("ERROR modbus_read_registers single");
		                        Console.WriteLine(string.Format("Address = {0}, value = {1} (0x{1:X2}) != {2} (0x{2:X2})",
		                               addr, core.RegisterRWUnsigned [addr], shortValues[addr]));
		                        ++nb_fail;
		                    }
		                }
		            }

		            // MULTIPLE REGISTERS
		            rc = core.RegistersRWWrite(addr, nb);
		            if (rc != nb) {
		                Console.WriteLine(string.Format("ERROR modbus_write_registers ({0})", rc));
		                Console.WriteLine(string.Format("Address = {0}, nb = {1}", addr, nb));
		                ++nb_fail;
		            } else {
						for (i=0; i<nb; ++i) core.RegisterRWUnsigned [i] = 0;

		                rc = core.RegistersRWRead(addr, nb);
		                if (rc != nb) {
		                    Console.WriteLine(string.Format("ERROR modbus_read_registers ({0})", rc));
		                    Console.WriteLine(string.Format("Address = {0}, nb = {1}", addr, nb));
		                    ++nb_fail;
		                } else {
		                    for (i=0; i<nb; ++i) {
		                        if (core.RegisterRWUnsigned [addr+i] != shortValues[addr+i]) {
		                            Console.WriteLine("ERROR modbus_read_registers");
		                            Console.WriteLine(string.Format("Address = {0}, value {1} (0x{1:X2}) != {2} (0x{2:X2})",
		                                   addr, core.RegisterRWUnsigned [addr+i], shortValues[addr+i]));
		                            ++nb_fail;
		                        }
		                    }
		                }
		            }

		            // R/W MULTIPLE REGISTERS
		            rc = core.RegistersRWWriteAndRead (addr, nb, addr, nb);
		            if (rc != nb) {
		                Console.WriteLine(string.Format("ERROR modbus_read_and_write_registers ({0})", rc));
		                Console.WriteLine(string.Format("Address = {0}, nb = {1}", addr, nb));
		                ++nb_fail;
		            } else {
						for (i=0; i<nb; ++i) core.RegisterRWUnsigned [i] = 0;
		                rc = core.RegistersRWRead(addr, nb);
		                if (rc != nb) {
		                    Console.WriteLine(string.Format("ERROR modbus_read_registers ({0})", rc));
		                    Console.WriteLine(string.Format("Address = {0}, nb = {1}\n", addr, nb));
		                    ++nb_fail;
		                } else {
		                    for (i=0; i<nb; ++i) {
		                        if (shortValues[addr+i] != core.RegisterRWUnsigned [addr+i]) {
			                        Console.WriteLine("ERROR modbus_read_and_write_registers READ");
			                        Console.WriteLine(string.Format("Address = {0}, value {1} (0x{1:X2}) != {2} (0x{2:X2})\n",
			                               addr, core.RegisterInputUnsigned[addr+i], core.RegisterRWUnsigned [addr+i]));
			                        ++nb_fail;
		                        }
		                    }
		                }
		            }
						
					
				}
			}

	        Console.Write("Test: ");
	        if (nb_fail>0)
	            Console.WriteLine(string.Format("{0} FAILS\n", nb_fail));
	        else
	            Console.WriteLine("SUCCESS");
			
			core.MappingDispose();
		    core.Close();
		    core.Dispose();
			
		    return 0;
		}



		public static int ServerTest (string listenAddress, int port, bool debug) {

			ModbusCore core = new ModbusCore (listenAddress, port);
			core.Debug = debug;

		    if (core.MappingNew (500, 500, 500, 500)) {
				Console.WriteLine ("Failed to allocate the mapping.");
		        return -1;
		    }

			// Initialization for testing
			for (int i = 0; i < 500; ++i) {
				core.RegisterRWSigned[i] = (short)(1000+i);  
				core.RegisterInputSigned[i] = (short)i;
			}

		    core.ListenTcp (1);
			while (true) {
				core.AcceptTcp ();

				while (true) {
			        byte[] query;
					try {
				        query = core.Receive ();
			            core.Reply (query);
					} catch { 
			            // Connection closed by the client or error
						break; 
					}
				}
			}
			
			core.MappingDispose ();
		    core.Close ();
		    core.Dispose ();
			
		    return 0;
		}

		public static int ServerTestMulti (string listenAddress, int port, bool debug) {

			ModbusCore core = new ModbusCore (listenAddress, port);
			core.Debug = debug;

		    if (core.MappingNew (500, 500, 500, 500)) {
				Console.WriteLine ("Failed to allocate the mapping.");
		        return -1;
		    }

			// Initialization for testing
			for (int i = 0; i < 500; ++i) {
				core.RegisterRWSigned[i] = (short)(1000+i);  
				core.RegisterInputSigned[i] = (short)i;
			}

		    core.ListenTcp (10);
			while (true) {
				core.AcceptTcp ();

				HandleModbusQuery mq = new HandleModbusQuery(core);
				Thread thread = new Thread(new ThreadStart(mq.HandleRequests));
				thread.Start ();
			}
			
			core.MappingDispose ();
		    core.Close ();
		    core.Dispose ();
			
		    return 0;
		}

		private class HandleModbusQuery {
			ModbusCore core = null;
			public HandleModbusQuery (ModbusCore core) {
				this.core = core;
			}

			public void HandleRequests () {
		        byte[] query;

				while (true) {
					try {
				        query = core.Receive ();
			            core.Reply (query);
					} catch { 
			            // Connection closed by the client or error
						break; 
					}
				}
			}
		}
	}
}
