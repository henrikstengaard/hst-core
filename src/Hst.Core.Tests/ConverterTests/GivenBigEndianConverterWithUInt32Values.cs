namespace Hst.Core.Tests.ConverterTests
{
    using System;
    using Converters;
    using Xunit;

    public class GivenBigEndianConverterWithUInt32Values
    {
        [Fact]
        public void When_ConvertBytesRepresentingUInt32MinValue_Then_ValueIsEqualToUInt32MinValue()
        {
            // arrange - uint32 min value
            const uint uInt32Min = 0;

            // arrange - bytes representing uint32 min value
            var bytes = new byte[] { 0, 0, 0, 0 };

            // act - convert bytes to uint32 value
            var uInt32Value = BigEndianConverter.ConvertBytesToUInt32(bytes);

            // assert - uint32 value is equal to min uint32
            Assert.Equal(uInt32Min, uInt32Value);
            Assert.Equal(uint.MinValue, uInt32Value);
        }

        [Fact]
        public void When_ConvertBytesRepresentingUInt32MaxValue_Then_ValueIsEqualToUInt32MaxValue()
        {
            // arrange - calculate uint32 max value
            const int uInt32Bits = 32;
            var uInt32Max = (uint)(Math.Pow(2, uInt32Bits) - 1);
            
            // arrange - bytes representing uint32 max value
            var bytes = new byte[] { 0xff, 0xff, 0xff, 0xff };

            // act - convert bytes to uint32 value
            var uInt32Value = BigEndianConverter.ConvertBytesToUInt32(bytes);

            // assert - uint32 value is equal to max uint32
            Assert.Equal(uInt32Max, uInt32Value);
        }


        [Fact]
        public void When_ConvertBytesRepresentingUInt32Value511_Then_ValueIsEqual()
        {
            // arrange - bytes representing uint32 value 511
            var bytes = new byte[] { 0, 0, 1, 0xff };

            // act - convert bytes to uint32 value
            var uInt32Value = BigEndianConverter.ConvertBytesToUInt32(bytes);

            // assert - uint32 value is equal to 511
            Assert.Equal(256U + 255, uInt32Value);
        }
        
        [Fact]
        public void When_ConvertUInt32Value511_Then_BytesAreEqual()
        {
            // arrange - bytes representing uint32 value 511
            var expectedBytes = new byte[]{0, 0, 1, 0xff};
            
            // act - convert uint32 value to bytes
            var bytes = new byte[4];
            BigEndianConverter.ConvertUInt32ToBytes(511, bytes);

            // assert - uint32 value is equal to 511
            Assert.Equal(expectedBytes, bytes);
        }
        
        [Fact]
        public void When_ConvertUInt32MaxValue_Then_BytesAreEqual()
        {
            // arrange - calculate uint32 max value
            const int uInt32Bits = 32;
            var uInt32Max = (uint)(Math.Pow(2, uInt32Bits) - 1);
            
            // arrange - bytes representing uint32 max value
            var expectedBytes = new byte[]{0xff, 0xff, 0xff, 0xff};
            
            // act - convert uint32 value to bytes
            var bytes = new byte[4];
            BigEndianConverter.ConvertUInt32ToBytes(uInt32Max, bytes);

            // assert - uint32 value is equal to max uint32
            Assert.Equal(expectedBytes, bytes);
        }
    }
}