namespace Hst.Core.Tests.ConverterTests
{
    using Converters;
    using Xunit;

    public class GivenSignedByteConverter
    {
        [Fact]
        public void WhenConvertByteValue1ToSignedByteThenResultIs1()
        {
            // arrange - unsigned byte value 1
            byte unsignedByte = 1;
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(unsignedByte);
            
            // assert - result is equal to 1
            Assert.Equal(1, result);
        }
        
        [Fact]
        public void WhenConvertByteValue127ToSignedByteThenResultIs127()
        {
            // arrange - unsigned byte value 127
            const byte unsignedByte = 127;
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(unsignedByte);
            
            // assert - result is equal to 127
            Assert.Equal(127, result);
        }
        
        [Fact]
        public void WhenConvertByteValue129ToSignedByteThenResultIsMinus127()
        {
            // arrange - unsigned byte value 129
            const byte unsignedByte = 129;
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(unsignedByte);
            
            // assert - result is equal to -127
            Assert.Equal(-127, result);
        }
        
        [Fact]
        public void WhenConvertByteValue255ToSignedByteThenResultIsMinus1()
        {
            // arrange - unsigned byte value 255
            const byte unsignedByte = 255;
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(unsignedByte);
            
            // assert - result is equal to -1
            Assert.Equal(-1, result);
        }
        
        [Fact]
        public void WhenConvertByteValue254ToSignedByteThenResultIsMinus2()
        {
            // arrange - array with unsigned byte value 254
            const byte unsignedByte = 254;
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(unsignedByte);
            
            // assert - result is equal to -2
            Assert.Equal(-2, result);
        }
        
        [Fact]
        public void WhenConvertBytesArrayWithValue129ToSignedByteThenResultIsMinus127()
        {
            // arrange - unsigned byte value 129
            var bytes = new byte[]{ 129 };
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(bytes);
            
            // assert - result is equal to 1
            Assert.Equal(-127, result);
        }
        
        [Fact]
        public void WhenConvertSignedByteValue1ToByteThenResultIs1()
        {
            // arrange - signed byte value 1
            const sbyte signedValue = 1;
            
            // act - convert signed byte to byte
            var result = SignedByteConverter.ConvertSignedByteToByte(signedValue);
            
            // assert - result is equal to 1
            Assert.Equal(1, result);
        }
        
        [Fact]
        public void WhenConvertSignedByteValue127ToByteThenResultIs127()
        {
            // arrange - signed byte value 127
            const sbyte signedValue = 127;
            
            // act - convert signed byte to byte
            var result = SignedByteConverter.ConvertSignedByteToByte(signedValue);
            
            // assert - result is equal to 127
            Assert.Equal(127, result);
        }
        
        [Fact]
        public void WhenConvertSignedByteValueMinus127ToSignedByteThenResultIs129()
        {
            // arrange - signed byte value -127
            const sbyte signedValue = -127;
            
            // act - convert signed byte to byte
            var result = SignedByteConverter.ConvertSignedByteToByte(signedValue);
            
            // assert - result is equal to 129
            Assert.Equal(129, result);
        }
        
        [Fact]
        public void WhenConvertSignedByteValueMinus1ToByteThenResultIs255()
        {
            // arrange - signed byte value -1
            const sbyte signedValue = -1;
            
            // act - convert signed byte to byte
            var result = SignedByteConverter.ConvertSignedByteToByte(signedValue);
            
            // assert - result is equal to 255
            Assert.Equal(255, result);
        }
        
        [Fact]
        public void WhenConvertSignedByteValueMinus2ToByteThenResultIs254()
        {
            // arrange - signed byte value -2
            const sbyte signedValue = -2;
            
            // act - convert signed byte to byte
            var result = SignedByteConverter.ConvertSignedByteToByte(signedValue);
            
            // assert - result is equal to 254
            Assert.Equal(254, result);
        }
        
        [Fact]
        public void WhenConvertSignedByteMinus1ToByteThenResultIsMinus255()
        {
            // arrange - signed byte value -1
            const sbyte signedValue = -1;
            
            // act - convert signed byte to byte
            var result = new byte[1];
            SignedByteConverter.ConvertSignedByteToByte(result, 0, signedValue);
            
            // assert - result is equal to 255
            Assert.Equal(255, result[0]);
        }
    }
}