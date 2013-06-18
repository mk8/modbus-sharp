using System;
using System.Runtime.InteropServices;
using Mono.Unix.Native;

namespace libmodbussharp
{
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct MODBUS_MAPPING {
	    public int nb_bits;						// Bit that can be read and write
	    public int nb_input_bits;				// Bit that can only read
	    public int nb_input_registers;			// Input Register 
	    public int nb_registers;				// Holding Register
	    public byte* tab_bits;
	    public byte* tab_input_bits;
	    public ushort* tab_input_registers;
	    public ushort* tab_registers;
	}

	public class ModbusCore : IDisposable
	{
		public static int MODBUS_TCP_MAX_ADU_LENGTH = 260;

		string AddressTarget;

		IntPtr modbusContext = IntPtr.Zero;
		IntPtr modbusResponseTimeout = IntPtr.Zero;
		IntPtr modbusByteTimeout = IntPtr.Zero;

		RegistersInputSigned   registerInputSigned;
		RegistersInputUnsigned registerInputUnsigned;
		RegistersRWSigned  	   registerRWSigned;
		RegistersRWUnsigned    registerRWUnsigned;
		BitsRW                 bitsRW;
		BitsInput              bitsInput;

		unsafe MODBUS_MAPPING* mapping = null;
		int socket;

		private void CheckContext() {
			if (modbusContext == IntPtr.Zero) {
				Exception ex = new Exception("Modbus context is not defined.");
				throw ex;
			}
		}

		private unsafe void CheckMapping() {
			if (mapping == null) {
				Exception ex = new Exception("Mapping is not defined.");
				throw ex;
			}
		}

		private void CheckForModbusError() {
			int errno = GetLastError ();
			
			if (errno!=0 && errno != 2 && errno != 115 && errno != 25) {
				Exception ex = new Exception ("ERRNO: " + errno + " - " + Error(errno));
				throw ex;
			}			
		}
		
		public void SetResponseTimeout(int usecTimeout){

			CheckContext();

			if (modbusResponseTimeout == IntPtr.Zero) {
				// At the moment I not know how to get the size of timeval struct so use a value big enought to contains timeval struct
				modbusResponseTimeout = Marshal.AllocHGlobal (32);
			}

			Timeval timeout = new Timeval();
			timeout.tv_usec = usecTimeout % 1000000;
			timeout.tv_sec = usecTimeout / 1000000;

			Mono.Unix.Native.NativeConvert.TryCopy (ref timeout, modbusResponseTimeout);

			ModbusPinvoke.SetResponseTimeout (modbusContext,modbusResponseTimeout);
		}
		
		public void SetByteTimeout(int usecTimeout){

			CheckContext();

			if (modbusByteTimeout == IntPtr.Zero) {
				// At the moment I not know how to get the size of timeval struct so use a value big enought to contains timeval struct
				modbusByteTimeout = Marshal.AllocHGlobal (32);
			}

			Timeval timeout = new Timeval();
			timeout.tv_usec = usecTimeout % 1000000;
			timeout.tv_sec = usecTimeout / 1000000;

			Mono.Unix.Native.NativeConvert.TryCopy (ref timeout, modbusByteTimeout);

			ModbusPinvoke.SetByteTimeout (modbusContext,modbusByteTimeout);
		}
		

		public void Close() {
			CheckContext();
			ModbusPinvoke.Close (modbusContext);
		}

		public void Dispose() {
			
			// Check if mapping still to exist
			try {
				Close ();
				MappingDispose ();
			} catch {}
			
			// Check if context still to exist
			if (modbusContext != IntPtr.Zero) {
				ModbusPinvoke.Free (modbusContext);
			}

			if (modbusByteTimeout != IntPtr.Zero) {
				Marshal.FreeHGlobal (modbusByteTimeout);
				modbusByteTimeout = IntPtr.Zero;
			}
			if (modbusResponseTimeout != IntPtr.Zero) {
				Marshal.FreeHGlobal (modbusResponseTimeout);
				modbusResponseTimeout = IntPtr.Zero;
			}

			modbusContext = IntPtr.Zero;
		}
		
		public bool Debug {
			set {
				CheckContext();
				ModbusPinvoke.SetDebug(modbusContext, value);
			}
		}
		
		public ModbusCore (string target, int baud, char pariry, int nBits, int stopBits)
		{			

			// Create modbus tcpip context
		    modbusContext = ModbusPinvoke.ModbusNewRtu(target, baud, pariry, nBits, stopBits);

			Initialize ();
		}
				
		public ModbusCore (string AddressTarget, int port)
		{

			this.AddressTarget = AddressTarget;

			// Create modbus tcpip context
		    modbusContext = ModbusPinvoke.ModbusNewTcp(AddressTarget, port);

			Initialize ();
		}

		void Initialize () {
			// Check on error in modbus library
			CheckForModbusError();

			registerInputSigned = new RegistersInputSigned(this);
			registerInputUnsigned = new RegistersInputUnsigned(this);

			registerRWSigned = new RegistersRWSigned(this);
			registerRWUnsigned = new RegistersRWUnsigned(this);

			bitsRW = new BitsRW(this);
			bitsInput = new BitsInput(this);
		}
		
		unsafe public bool MappingNew(int NBit, int NInputBit, int NHoldingRegisters, int NInputRegister) {

			// Check for duplicate mappings
			if (mapping != null) {
				Exception ex=new Exception("Duplicate mapping creation.");
				throw ex;
			}
			
			// Create new mapping
			mapping = ModbusPinvoke.MappingNew(NBit, NInputBit, NHoldingRegisters, NInputRegister);
			
			return mapping == null;
		}

		unsafe public void MappingDispose() {

			// Check for exsisting mappings
			if (mapping == null) {
				Exception ex=new Exception("Mapping is not created.");
				throw ex;
			}
			
			// Dispose mappings
			ModbusPinvoke.MappingDispose(mapping);
			mapping = null;
		}
				
		unsafe public bool Connect() {
			int ret;

			ret = ModbusPinvoke.Connect(modbusContext);
			
			if (ret != 0) {
				Close ();
			}

			return ret == 0;
		}

		unsafe public void ListenTcp(int maxWaitingQueue) {
			CheckContext();
			socket = ModbusPinvoke.ListenTcp(modbusContext,maxWaitingQueue);
		}

		unsafe public void AcceptTcp() {
			int ret=0;
			
			// Check for context
			CheckContext();
			CheckMapping ();

			// Check for opening socket
			if (socket == 0) {
				Exception ex = new Exception("Start TCP listener before accept connection");
				throw ex;
			}
			
			// Wainting for incoming tcp connection
			fixed (int* ptr = &socket) {
				ret = ModbusPinvoke.AcceptTcp(modbusContext,ptr);
			}
			
			// Check about the success of the incoming tcp connection
			if (ret < 0) {
				CheckForModbusError();
			}
		}

		unsafe public byte[] Receive() {
			int res;
			byte[] query = new byte[ModbusCore.MODBUS_TCP_MAX_ADU_LENGTH];

			CheckContext();
			if (socket==0) {
				Exception ex = new Exception("Socket is not open.");
				throw ex;
			}

			fixed (byte* queryArray= &query[0]) {
				res=ModbusPinvoke.Receive(modbusContext, queryArray);
			}
			if (res<0) {
				CheckForModbusError();
			}
				
			Array.Resize<byte>(ref query,res);
			return query;
		}
		
		unsafe public int Reply(byte[] query) {
			int res;
			CheckContext ();
			CheckMapping ();
			if (socket==0) {
				Exception ex = new Exception("The socket is not open.");
				throw ex;
			}

			fixed (byte* queryArray= &query[0]) {
				res=ModbusPinvoke.Reply(modbusContext, queryArray, query.Length, (IntPtr) mapping);
			}
			if (res<0) {
				CheckForModbusError();
			}
			return res;
		}


		public int RegistersInputRead(int start) {
			return RegistersInputRead(start, 1);
		}
		
		unsafe public int RegistersInputRead(int start, int length) {
		
			int res =0;

			CheckMapping ();

			if (start+length > mapping->nb_input_registers) {
				Exception ex=new Exception("Trying to access outside defined memory space for input_registers");
				throw ex;
			}

			res=ModbusPinvoke.RegistersInputRead(modbusContext, start,length,&mapping->tab_input_registers[start]);
			if (res<0) {
				CheckForModbusError();
			}
			return res;
		}

		public int RegistersRWRead (int start) {
			return RegistersRWRead (start,1);
		}
		
		unsafe public int RegistersRWRead (int start, int length) {
		
			int res =0;

			CheckMapping ();

			if (start+length > mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside defined memory space for registers");
				throw ex;
			}
			
			res=ModbusPinvoke.RegistersRWRead(modbusContext, start,length,&mapping->tab_registers[start]);
			
			if (res<0) {
				CheckForModbusError();
			}
			return res;
		}

		public int RegistersRWWrite (int index) {
			return RegistersRWWrite (index, 1);
		}
		
		public unsafe int RegistersRWWrite(int index, int length) {
			CheckContext ();
			CheckMapping ();
			if (index+length > mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			int ret = ModbusPinvoke.RegistersRWWrite (modbusContext, index, length, &mapping->tab_registers[index]);
			if (ret<0) {
				CheckForModbusError ();
			}
			return ret;
		}

		public unsafe int RegistersRWWriteAndRead(int readIndex, int readLength, int writeIndex, int writeLength) {
			CheckContext ();
			CheckMapping ();
			if (readIndex+readLength > mapping->nb_input_registers) {
				Exception ex=new Exception("Trying to access outside the input_registers memory space.");
				throw ex;
			}
			if (writeIndex+writeLength > mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			int ret = ModbusPinvoke.RegistersRWWriteAndRead(modbusContext, 
			                                              readIndex, readLength, &mapping->tab_registers[readIndex],
			                                              writeIndex, writeLength, &mapping->tab_registers[writeIndex]);
			if (ret<0) {
				CheckForModbusError();
			}
			return ret;
		}

		public unsafe float GetRegisterInputAsFloat(int index) {
			CheckMapping ();
			if (index+1 > mapping->nb_input_registers) {
				Exception ex = new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			
			ushort[] buffer = new ushort[2];
			buffer[1] = mapping->tab_input_registers[index];
			buffer[0] = mapping->tab_input_registers[index+1];
			fixed (ushort* p = &buffer[0]) {
				return ModbusPinvoke.GetFloat(p);
			}
		}

		public unsafe float GetRegisterRWAsFloat(int index) {
			CheckMapping ();
			if (index+1 > mapping->nb_registers) {
				Exception ex = new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
				
			ushort[] buffer = new ushort[2];
			buffer[1] = mapping->tab_registers[index];
			buffer[0] = mapping->tab_registers[index+1];
			fixed (ushort* p = &buffer[0]) {
				return ModbusPinvoke.GetFloat(p);
			}
		}

		public unsafe void SetRegisterRWAsFloat(int index, float val) {
			CheckMapping ();
			if (index+1 > mapping->nb_registers) {
				Exception ex = new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
				
			ushort[] buffer = new ushort[2];
			fixed (ushort* p = &buffer[0]) {
				ModbusPinvoke.SetFloat(val, p);
			}
			
			mapping->tab_registers[index] = buffer[1];
			mapping->tab_registers[index+1] = buffer[0];
		}

		unsafe public bool BitsRWRead(int start) {
			BitsRWRead(start,1);
			return (mapping->tab_bits[start] == 1);
		}
		
		unsafe public int BitsRWRead(int start, int length) {
		
			int res =0;

			CheckMapping ();

			if (start+length > mapping->nb_bits) {
				Exception ex=new Exception("Trying to access outside defined memory space for bits");
				throw ex;
			}
			
			res=ModbusPinvoke.BitsRWRead(modbusContext, start,length,&mapping->tab_bits[start]);
			if (res<0) {
				CheckForModbusError();
			}
			return res;
		}

		public int BitsRWWrite(int index) {
			return BitsRWWrite(index,1);
		}
		
		public unsafe int BitsRWWrite(int index, int length) {
			CheckContext ();
			CheckMapping ();
			if (index+length > mapping->nb_bits) {
				Exception ex=new Exception("Trying to access outside the bits memory space.");
				throw ex;
			}
			int ret = ModbusPinvoke.BitsRWWrite(modbusContext, index, length, &mapping->tab_bits[index]);
			if (ret<0) {
				CheckForModbusError();
			}
			return ret;
		}

		unsafe public bool BitsInputRead(int start) {
			BitsInputRead(start,1);
			return mapping->tab_input_bits[start] == 1;
		}

		unsafe public int BitsInputRead(int start, int length) {
		
			int res =0;

			CheckMapping ();

			if (start+length > mapping->nb_bits) {
				Exception ex=new Exception("Trying to access outside defined memory space for bits");
				throw ex;
			}
			res=ModbusPinvoke.BitsInputRead(modbusContext, start,length,&mapping->tab_input_bits[start]);
			if (res<0) {
				CheckForModbusError();
			}
			return res;
		}


		public RegistersInputSigned RegisterInputSigned {
			get {
				return registerInputSigned;
			}
		}

		public RegistersInputUnsigned RegisterInputUnsigned {
			get {
				return registerInputUnsigned;
			}
		}

		public RegistersRWSigned RegisterRWSigned {
			get {
				return registerRWSigned;
			}
		}

		public RegistersRWUnsigned RegisterRWUnsigned {
			get {
				return registerRWUnsigned;
			}
		}

		public BitsRW BitsRW {
			get {
				return bitsRW;
			}
		}

		public BitsInput BitsInput {
			get {
				return bitsInput;
			}
		}

		internal unsafe ushort GetRegisterInputUnsigned (int index) {
			CheckContext ();
			CheckMapping ();
			if (index>=mapping->nb_input_registers) {
				Exception ex=new Exception("Trying to access outside the input_registers memory space.");
				throw ex;
			}
			return mapping->tab_input_registers[index];
		}

		internal unsafe short GetRegisterInputSigned(int index) {
			CheckContext ();
			CheckMapping ();
			if (index>=mapping->nb_input_registers) {
				Exception ex=new Exception("Trying to access outside the input_registers memory space.");
				throw ex;
			}
			return (short)mapping->tab_input_registers[index];
		}

		internal unsafe ushort GetRegisterRWUnsigned(int index) {
			CheckContext ();
			CheckMapping ();
			if (index>=mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			return mapping->tab_registers[index];
		}
		
		internal unsafe short GetRegisterRWSigned(int index) {
			CheckContext ();
			CheckMapping ();
			if (index>=mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			return (short)mapping->tab_registers[index];
		}

		internal unsafe void SetRegisterRW(int index, ushort val) {
			CheckContext ();
			CheckMapping ();
			if (index>=mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			mapping->tab_registers[index]=val;
		}

		internal unsafe void SetRegisterRW(int index, short val) {
			CheckContext ();
			CheckMapping ();
			if (index>=mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			mapping->tab_registers[index]=(ushort)val;
		}

		internal unsafe void SetRegisterInput(int index, ushort val) {
			CheckContext ();
			CheckMapping ();
			if (index>=mapping->nb_input_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			mapping->tab_input_registers[index]=val;
		}

		internal unsafe void SetRegisterInput(int index, short val) {
			CheckContext ();
			CheckMapping ();
			if (index>=mapping->nb_input_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			mapping->tab_input_registers[index]=(ushort)val;
		}

		internal unsafe bool GetBitsRW(int index) {
			CheckContext ();
			CheckMapping ();
			if (index>=mapping->nb_bits) {
				Exception ex=new Exception("Trying to access outside the bits memory space.");
				throw ex;
			}
			return mapping->tab_bits[index] != 0;
		}

		internal unsafe bool GetBitsInput (int index) {
			CheckContext ();
			CheckMapping ();
			if (index >= mapping->nb_input_bits) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			
			return mapping->tab_input_bits[index] != 0;
		}

		internal unsafe void SetBitsRW(int index, bool val) {
			CheckContext ();
			CheckMapping ();
			if (index >= mapping->nb_bits) {
				Exception ex=new Exception("Trying to access outside the bits memory space.");
				throw ex;
			}
			mapping->tab_bits[index]=(byte)(val ? 1 : 0);
		}

		internal unsafe void SetBitsInput(int index, bool val) {
			CheckContext ();
			CheckMapping ();
			if (index >= mapping->nb_input_bits) {
				Exception ex=new Exception("Trying to access outside the bits memory space.");
				throw ex;
			}
			mapping->tab_input_bits[index]=(byte)(val ? 1 : 0);
		}




		unsafe public int DirectWriteBitRW (int index, bool val) {
			CheckContext ();
			CheckMapping ();
			if (index >= mapping->nb_bits) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			int ret = ModbusPinvoke.WriteBit (modbusContext, index, (val ? 1 : 0));
			if (ret<0) {
				CheckForModbusError();
			}
			return ret;			
		}

		public unsafe int DirectWriteRegisterRW(int index, ushort val) {
			CheckContext ();
			CheckMapping ();
			if (index >= mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			int ret = ModbusPinvoke.WriteRegisterRW(modbusContext, index, val);
			if (ret<0) {
				CheckForModbusError();
			}
			return ret;
		}
		
		public unsafe int DirectWriteRegisterRW(int index, short val) {
			CheckContext ();
			CheckMapping ();
			if (index >= mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}
			int ret = ModbusPinvoke.WriteRegisterRW(modbusContext, index, val);
			if (ret<0) {
				CheckForModbusError();
			}
			return ret;
		}

		public unsafe int DirectWriteRegisterRW(int index, Single val) {
			CheckContext ();
			CheckMapping ();
			if (index >= mapping->nb_registers) {
				Exception ex=new Exception("Trying to access outside the registers memory space.");
				throw ex;
			}

							
			ushort[] buffer = new ushort[2];
			fixed (ushort* p = &buffer[0]) {
				ModbusPinvoke.SetFloat(val, p);
			}
			
			int ret = ModbusPinvoke.WriteRegisterRW(modbusContext, index, buffer [1]);
			if (ret != 0) {
				ret = ModbusPinvoke.WriteRegisterRW(modbusContext, index + 1, buffer [0]);
			}
			if (ret<0) {
				CheckForModbusError();
			}
			return ret;
		}

		public unsafe string Error(int errno) {
			IntPtr prt = ModbusPinvoke.Error(errno);
			return Marshal.PtrToStringAnsi(prt);
		}

		public unsafe void SetSlave(int slave) {
			ModbusPinvoke.SetSlave(modbusContext, slave);
		}

		public unsafe int SetRsuRts(int flag) {
			int ret = ModbusPinvoke.RtuSetRts(modbusContext, flag);
			if (ret<0) {
				CheckForModbusError();
			}
			return ret;
		}
		
		private int GetLastError () {
			 return Marshal.GetLastWin32Error ();
		}

		private class ModbusPinvoke {			
			[DllImport("libmodbus.so", EntryPoint="modbus_new_tcp", SetLastError=true)]
			static internal extern IntPtr ModbusNewTcp([MarshalAs(UnmanagedType.LPStr)] string target,int port);
			
			[DllImport("libmodbus.so", EntryPoint="modbus_tcp_listen", SetLastError=true)]
			static internal extern int ListenTcp(IntPtr context,int maxWaitingConnection);
				
			[DllImport("libmodbus.so", EntryPoint="modbus_tcp_accept", SetLastError=true)]
			static unsafe internal extern int AcceptTcp(IntPtr context,int* socket);

			[DllImport("libmodbus.so", EntryPoint="modbus_mapping_new", SetLastError=true)]
			unsafe static internal extern MODBUS_MAPPING* MappingNew(int NBit, int NInputBit, int NInputRegisters, int NInput);		
	
			[DllImport("libmodbus.so", EntryPoint="modbus_mapping_free", SetLastError=true)]
			unsafe static internal extern void MappingDispose(MODBUS_MAPPING* map);		
	
			[DllImport("libmodbus.so", EntryPoint="modbus_connect", SetLastError=true)]
			unsafe static internal extern int Connect(IntPtr context);		

			[DllImport("libmodbus.so", EntryPoint="modbus_close", SetLastError=true)]
			unsafe static internal extern void Close(IntPtr context);		

			[DllImport("libmodbus.so", EntryPoint="modbus_free", SetLastError=true)]
			unsafe static internal extern void Free(IntPtr context);		

			[DllImport("libmodbus.so", EntryPoint="modbus_read_input_registers", SetLastError=true)]
			unsafe static internal extern int RegistersInputRead(IntPtr context, int start, int length, ushort* buffer);
		
			[DllImport("libmodbus.so", EntryPoint="modbus_read_input_registers", SetLastError=true)]
			unsafe static internal extern int ReadInputRegistersSigned(IntPtr context, int start, int length, short* buffer);
		
			[DllImport("libmodbus.so", EntryPoint="modbus_read_registers", SetLastError=true)]
			unsafe static internal extern int RegistersRWRead(IntPtr context, int start, int length, ushort* buffer);
		
			[DllImport("libmodbus.so", EntryPoint="modbus_read_registers", SetLastError=true)]
			unsafe static internal extern int ReadRegistersSigned(IntPtr context, int start, int length, short* buffer);
		
			[DllImport("libmodbus.so", EntryPoint="modbus_write_register", SetLastError=true)]
			unsafe static internal extern int WriteRegisterRW(IntPtr context, int addr, ushort val);

			[DllImport("libmodbus.so", EntryPoint="modbus_write_register", SetLastError=true)]
			unsafe static internal extern int WriteRegisterRW(IntPtr context, int addr, short val);

			[DllImport("libmodbus.so", EntryPoint="modbus_write_registers", SetLastError=true)]
			unsafe static internal extern int RegistersRWWrite(IntPtr context, int addr, int length, ushort* buffer);

			[DllImport("libmodbus.so", EntryPoint="modbus_write_and_read_registers", SetLastError=true)]
			unsafe static internal extern int RegistersRWWriteAndRead(IntPtr context, int readAddr, int readLength, ushort* readBuffer, int writeAddr, int writeLegth, ushort* writeBuffer);
			
			[DllImport("libmodbus.so", EntryPoint="modbus_write_bits", SetLastError=true)]
			unsafe static internal extern int BitsRWWrite(IntPtr context, int addr, int len, byte* buffer);

			[DllImport("libmodbus.so", EntryPoint="modbus_write_bit", SetLastError=true)]
			unsafe static internal extern int WriteBit(IntPtr context, int addr, int status);

			[DllImport("libmodbus.so", EntryPoint="modbus_read_bits", SetLastError=true)]
			unsafe static internal extern int BitsRWRead(IntPtr context, int addr, int len, byte* buffer);
				
			[DllImport("libmodbus.so", EntryPoint="modbus_read_input_bits", SetLastError=true)]
			unsafe static internal extern int BitsInputRead(IntPtr context, int addr, int len, byte* buffer);
				
			[DllImport("libmodbus.so", EntryPoint="modbus_set_debug", SetLastError=true)]
		    unsafe static internal extern void SetDebug(IntPtr context, bool debug);
			
			[DllImport("libmodbus.so", EntryPoint="modbus_receive", SetLastError=true)]
			unsafe static internal extern int Receive(IntPtr context, byte* req);
			
			[DllImport("libmodbus.so", EntryPoint="modbus_reply", SetLastError=true)]
			unsafe static internal extern int Reply(IntPtr context, byte* req, int len, IntPtr mapping);

			[DllImport("libmodbus.so", EntryPoint="modbus_strerror", SetLastError=true)]
			unsafe static internal extern IntPtr Error(int errno);

			[DllImport("libmodbus.so", EntryPoint="modbus_set_slave", SetLastError=true)]
			unsafe static internal extern void SetSlave(IntPtr context, int slave);

			[DllImport("libmodbus.so", EntryPoint="modbus_get_float", SetLastError=true)]
			unsafe static internal extern float GetFloat(ushort* buffer);
			
			[DllImport("libmodbus.so", EntryPoint="modbus_set_float", SetLastError=true)]
			unsafe static internal extern float SetFloat(float val, ushort* buffer);

			//[DllImport("libmodbus.so", EntryPoint="modbus_rtu_set_time_rts_switch", SetLastError=true)]
			//unsafe static internal extern float SetResponseTimeout(IntPtr context, Int32 usec);
						
 			[DllImport("libmodbus.so", EntryPoint="modbus_rtu_set_rts", SetLastError=true)]
			unsafe static internal extern int RtuSetRts(IntPtr context, int flag); 
			
			[DllImport("libmodbus.so", EntryPoint="modbus_new_rtu", SetLastError=true)]
			static internal extern IntPtr ModbusNewRtu([MarshalAs(UnmanagedType.LPStr)] string target, int baud, char pariry, int nBits, int stopBits);

			[DllImport("libmodbus.so", EntryPoint="modbus_set_response_timeout")]
			static internal extern IntPtr SetResponseTimeout(IntPtr context, IntPtr timeout);

			[DllImport("libmodbus.so", EntryPoint="modbus_set_byte_timeout")]
			static internal extern IntPtr SetByteTimeout(IntPtr context, IntPtr timeout);

		}
	}
}
