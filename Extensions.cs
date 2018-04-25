using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CSExtensions {
    /// <summary>
    /// Specifies the number of bits in the bit field structure
    /// Maximum number of bits are 64
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class BitFieldNumberOfBitsAttribute : Attribute {
        /// <summary>
        /// Initializes new instance of BitFieldNumberOfBitsAttribute with the specified number of bits
        /// </summary>
        /// <param name="bitCount">The number of bits the bit field will contain (Max 64)</param>
        public BitFieldNumberOfBitsAttribute(byte bitCount) {
            if ((bitCount < 1) || (bitCount > 64))
                throw new ArgumentOutOfRangeException("bitCount", bitCount,
                "The number of bits must be between 1 and 64.");

            BitCount = bitCount;
        }

        /// <summary>
        /// The number of bits the bit field will contain
        /// </summary>
        public byte BitCount { get; private set; }
    }

    /// <summary>
    /// Specifies the length of each bit field
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class BitFieldInfoAttribute : Attribute {
        /// <summary>
        /// Initializes new instance of BitFieldInfoAttribute with the specified field offset and length
        /// </summary>
        /// <param name="offset">The offset of the bit field</param>
        /// <param name="length">The number of bits the bit field occupies</param>
        public BitFieldInfoAttribute(byte offset, byte length) {
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// The offset of the bit field
        /// </summary>
        public byte Offset { get; private set; }

        /// <summary>
        /// The number of bits the bit field occupies
        /// </summary>
        public byte Length { get; private set; }
    }

    public static class BitFeildMethods {
        /// <summary>
        /// Creates a new instance of the provided struct.
        /// </summary>
        /// <typeparam name="T">The type of the struct that is to be created.</typeparam>
        /// <param name="value">The initial value of the struct.</param>
        /// <returns>The instance of the new struct.</returns>
        public static T CreateBitField<T>(ulong value) where T : struct {
            // The created struct has to be boxed, otherwise PropertyInfo.SetValue
            // will work on a copy instead of the actual object
            object boxedValue = new T();

            // Loop through the properties and set a value to each one
            foreach (PropertyInfo pi in boxedValue.GetType().GetProperties()) {
                BitFieldInfoAttribute bitField;
                bitField = (pi.GetCustomAttribute(typeof(BitFieldInfoAttribute)) as BitFieldInfoAttribute);
                if (bitField != null) {
                    ulong mask = (ulong)Math.Pow(2, bitField.Length) - 1;
                    object setVal = Convert.ChangeType((value >> bitField.Offset) & mask, pi.PropertyType);
                    pi.SetValue(boxedValue, setVal);
                }
            }
            // Unboxing the object
            return (T)boxedValue;
        }

        ///// <summary>
        ///// Creates a new instance of the provided struct.
        ///// </summary>
        ///// <typeparam name="T">The type of the struct that is to be created.</typeparam>
        ///// <param name="value">The initial value of the struct.</param>
        ///// <returns>The instance of the new struct.</returns>
        //public static T CreateBitField<T>(byte[] value) where T : struct {
        //    // The created struct has to be boxed, otherwise PropertyInfo.SetValue
        //    // will work on a copy instead of the actual object
        //    object boxedValue = new T();

        //    // Loop through the properties and set a value to each one
        //    foreach (PropertyInfo pi in boxedValue.GetType().GetProperties()) {
        //        BitFieldInfoAttribute bitField;
        //        bitField = (pi.GetCustomAttribute(typeof(BitFieldInfoAttribute)) as BitFieldInfoAttribute);
        //        if (bitField != null) {
        //            ulong mask = (ulong)Math.Pow(2, bitField.Length) - 1;
        //            object setVal = Convert.ChangeType((value >> bitField.Offset) & mask, pi.PropertyType);
        //            pi.SetValue(boxedValue, setVal);
        //        }
        //    }
        //    // Unboxing the object
        //    return (T)boxedValue;
        //}

        /// <summary>
        /// This method converts the struct into a string of binary values.
        /// The length of the string will be equal to the number of bits in the struct.
        /// The least significant bit will be on the right in the string.
        /// </summary>
        /// <param name="obj">An instance of a struct that implements the interface IBitField.</param>
        /// <returns>A string representing the binary value of tbe bit field.</returns>
        public static string ToBinaryString(this IBitField obj) {
            BitFieldNumberOfBitsAttribute bitField;
            bitField = (obj.GetType().GetCustomAttribute(typeof(BitFieldNumberOfBitsAttribute)) as BitFieldNumberOfBitsAttribute);
            if (bitField == null)
                throw new Exception(string.Format(@"The attribute 'BitFieldNumberOfBitsAttribute' has to be 
            added to the struct '{0}'.", obj.GetType().Name));

            StringBuilder sb = new StringBuilder(bitField.BitCount);

            ulong bitFieldValue = obj.ToUInt64();
            for (int i = bitField.BitCount - 1; i >= 0; i--) {
                sb.Append(((bitFieldValue & (1UL << i)) > 0) ? "1" : "0");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts the members of the bit field to an integer value.
        /// </summary>
        /// <param name="obj">An instance of a struct that implements the interface IBitField.</param>
        /// <returns>An integer representation of the bit field.</returns>
        public static ulong ToUInt64(this IBitField obj) {
            ulong result = 0;

            // Loop through all the properties
            foreach (PropertyInfo pi in obj.GetType().GetProperties()) {
                // Check if the property has an attribute of type BitFieldLengthAttribute
                BitFieldInfoAttribute bitField;
                bitField = (pi.GetCustomAttribute(typeof(BitFieldInfoAttribute)) as BitFieldInfoAttribute);
                if (bitField != null) {
                    // Calculate a bitmask using the length of the bit field
                    ulong mask = 0;
                    for (byte i = 0; i < bitField.Length; i++)
                        mask |= 1UL << i;

                    // This conversion makes it possible to use different types in the bit field
                    ulong value = Convert.ToUInt64(pi.GetValue(obj));

                    result |= (value & mask) << bitField.Offset;
                }
            }

            return result;
        }

        // notes: this method is basically forcing byte arrays into datastructures
        // this is a little bit insane and is considered totally unsafe in C#, but that's what we're doing
        // updates: this works perfectly, don't question it
        public static unsafe T PassData<T>(ushort flag, ref ushort enable, byte[] buff, int datalen, ref uint offset) where T : struct {
            byte[] dataToFill;
            if ((flag & enable) > 0) {
                dataToFill = buff.Skip((int)offset).Take((int)datalen).ToArray();                
                offset += (uint)datalen;

                float test;
                if ((uint)datalen > 10) {
                    test = BitConverter.ToSingle(dataToFill, 0);
                    test = SwapEndianness(test);
                }

                enable <<= 1;
                unsafe {
                    fixed (byte* p = &dataToFill[0]) {
                        return (T)Marshal.PtrToStructure(new IntPtr(p), typeof(T));
                    }
                }
            }

            enable <<= 1;
            // return null
            return default(T);
        }

        public static float SwapEndianness(float x) {
            // get the raw bytes, invert, and set away, ugly but works
            byte[] temp = BitConverter.GetBytes(x);
            Array.Reverse(temp);
            return BitConverter.ToSingle(temp, 0);
        }

        public static float SwapEndianness(int x) {
            // get the raw bytes, invert, and set away, ugly but works
            byte[] temp = BitConverter.GetBytes(x);
            Array.Reverse(temp);
            return BitConverter.ToInt32(temp, 0);
        }

        public static float SwapEndianness(short x) {
            // get the raw bytes, invert, and set away, ugly but works
            byte[] temp = BitConverter.GetBytes(x);
            Array.Reverse(temp);
            return BitConverter.ToInt16(temp, 0);
        }

        public static uint SwapEndianness(uint x) {
            return ((x & 0x000000ff) << 24) +  // First byte
                   ((x & 0x0000ff00) << 8) +   // Second byte
                   ((x & 0x00ff0000) >> 8) +   // Third byte
                   ((x & 0xff000000) >> 24);   // Fourth byte
        }
        public static ushort SwapEndianness(ushort x) {
            return (ushort)(((x & 0x00ff) << 8) +  // First byte
                   ((x & 0xff00) >> 8));   // Fourth byte
        }
    }

    public class FixedSizedQueue<T> : ConcurrentQueue<T> {
        private readonly object syncObject = new object();

        public int Size { get; private set; }

        public FixedSizedQueue(int size) {
            Size = size;
        }

        public new void Enqueue(T obj) {
            base.Enqueue(obj);
            lock (syncObject) {
                while (base.Count > Size) {
                    T outObj;
                    base.TryDequeue(out outObj);
                }
            }
        }
    }

    /// <summary>
    /// Interface used as a marker in order to create extension methods on a struct
    /// that is used to emulate bit fields
    /// </summary>
    public interface IBitField { }
}
