using System;

namespace libmodbussharp
{
	public class RegistersInputSigned {
		ModbusCore core = null;

		internal RegistersInputSigned (ModbusCore core) {
			this.core = core;
		}

		public Int16 this [int indexer] {
			get {
				return core.GetRegisterInputSigned(indexer);
			}
			set {
				core.SetRegisterInput(indexer, value);
			}

		}
	}

	public class RegistersInputUnsigned {
		ModbusCore core = null;

		internal RegistersInputUnsigned (ModbusCore core) {
			this.core = core;
		}

		public UInt16 this [int indexer] {
			get {
				return core.GetRegisterInputUnsigned(indexer);
			}
			set {
				core.SetRegisterInput(indexer, value);
			}
		}
	}

	public class RegistersRWSigned {
		ModbusCore core = null;

		internal RegistersRWSigned (ModbusCore core) {
			this.core = core;
		}

		public Int16 this [int indexer] {
			get {
				return core.GetRegisterRWSigned(indexer);
			}
			set {
				core.SetRegisterRW(indexer, value);
			}
		}
	}

	public class RegistersRWUnsigned {
		ModbusCore core = null;

		internal RegistersRWUnsigned (ModbusCore core) {
			this.core = core;
		}

		public UInt16 this [int indexer] {
			get {
				return core.GetRegisterRWUnsigned(indexer);
			}
			set {
				core.SetRegisterRW(indexer, value);
			}
		}
	}

	public class BitsRW {
		ModbusCore core = null;

		internal BitsRW (ModbusCore core) {
			this.core = core;
		}

		public bool this [int indexer] {
			get {
				return core.GetBitsRW(indexer);
			}
			set {
				core.SetBitsRW (indexer, value);
			}
		}
	}

	public class BitsInput {
		ModbusCore core = null;

		internal BitsInput (ModbusCore core) {
			this.core = core;
		}

		public bool this [int indexer] {
			get {
				return core.GetBitsInput(indexer);
			}
			set {
				core.SetBitsInput (indexer, value);
			}
		}
	}
}

