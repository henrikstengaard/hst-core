namespace Hst.Core.Tests.ConverterTests
{
    using Converters;
    using Xunit;

    public class GivenSignedByteConverter
    {
        [Fact]
        public void WhenConvertByte1ToSignedByteThenResultIs1()
        {
            // arrange - array with unsigned byte value 1
            var bytes = new byte[]{ 1 };
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(bytes);
            
            // assert - result is equal to 1
            Assert.Equal(1, result);
        }
        
        [Fact]
        public void WhenConvertByte127ToSignedByteThenResultIs127()
        {
            // arrange - array with unsigned byte value 127
            var bytes = new byte[]{ 127 };
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(bytes);
            
            // assert - result is equal to 127
            Assert.Equal(127, result);
        }
        
        [Fact]
        public void WhenConvertByte129ToSignedByteThenResultIsMinus127()
        {
            // arrange - array with unsigned byte value 129
            var bytes = new byte[]{ 129 };
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(bytes);
            
            // assert - result is equal to -1
            Assert.Equal(-127, result);
        }
        
        [Fact]
        public void WhenConvertByte255ToSignedByteThenResultIsMinus1()
        {
            // arrange - array with unsigned byte value 255
            var bytes = new byte[]{ 255 };
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(bytes);
            
            // assert - result is equal to -1
            Assert.Equal(-1, result);
        }
        
        [Fact]
        public void WhenConvertByte254ToSignedByteThenResultIsMinus2()
        {
            // arrange - array with unsigned byte value 254
            var bytes = new byte[]{ 254 };
            
            // act - convert byte to signed byte
            var result = SignedByteConverter.ConvertByteToSignedByte(bytes);
            
            // assert - result is equal to -2
            Assert.Equal(-2, result);
        }
    }
}