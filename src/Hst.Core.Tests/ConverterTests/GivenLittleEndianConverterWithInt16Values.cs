using System;
using Hst.Core.Converters;
using Xunit;

namespace Hst.Core.Tests.ConverterTests;

public class GivenLittleEndianConverterWithInt16Values
{
    [Fact]
    public void When_ConvertBytesRepresentingInt16MinValue_Then_ValueIsEqualToInt16MinValue()
    {
        // arrange - calculate int16 min value
        const int int16Bits = 16;
        var int16Min = Math.Pow(2, int16Bits) / 2 * -1;

        // arrange - bytes representing int16 min value
        var bytes = new byte[] { 0, 0x80 };

        // act - convert bytes to int16 value
        var int16Value = LittleEndianConverter.ConvertBytesToInt16(bytes);

        // assert - int16 value is equal to min int16
        Assert.Equal(int16Min, int16Value);
        Assert.Equal(short.MinValue, int16Value);
    }

    [Fact]
    public void When_ConvertBytesRepresentingInt16MaxValue_Then_ValueIsEqualToInt16MaxValue()
    {
        // arrange - calculate int16 max value
        const int int16Bits = 16;
        var int16Max = (Math.Pow(2, int16Bits) / 2) - 1;

        // arrange - bytes representing int16 max value
        var bytes = new byte[] { 0xff, 0x7f };

        // act - convert bytes to int16 value
        var int16Value = LittleEndianConverter.ConvertBytesToInt16(bytes);

        // assert - int16 value is equal to max int16
        Assert.Equal(int16Max, int16Value);
        Assert.Equal(short.MaxValue, int16Value);
    }

    [Fact]
    public void When_ConvertBytesRepresentingInt16Value511_Then_ValueIsEqual()
    {
        // arrange - bytes representing int16 value 511
        var bytes = new byte[] { 0xff, 0x1 };

        // act - convert bytes to int16 value
        var int16Value = LittleEndianConverter.ConvertBytesToInt16(bytes);

        // assert - int16 value is equal to 511
        Assert.Equal(256 + 255, int16Value);
    }

    [Fact]
    public void When_ConvertInt16Value511_Then_BytesAreEqual()
    {
        // arrange - bytes representing int16 value 511
        var expectedBytes = new byte[]{ 0xff, 0x1 };
            
        // act - convert int16 value to bytes
        var bytes = new byte[2];
        LittleEndianConverter.ConvertInt16ToBytes(511, bytes);

        // assert - int16 value is equal to 511
        Assert.Equal(expectedBytes, bytes);
    }

    [Fact]
    public void When_ConvertInt16MaxValue_Then_BytesAreEqual()
    {
        // arrange - bytes representing int16 max value
        var expectedBytes = new byte[] { 0xff, 0x7f };

        // arrange - calculate int16 max value
        const int int16Bits = 16;
        var int16Max = (short)((Math.Pow(2, int16Bits) / 2) - 1);
            
        // act - convert int16 value to bytes
        var bytes = new byte[2];
        LittleEndianConverter.ConvertInt16ToBytes(int16Max, bytes);

        // assert - bytes are is equal to expected int16 max value bytes
        Assert.Equal(expectedBytes, bytes);
    }
        
    [Fact]
    public void When_ConvertInt16MinValue_Then_BytesAreEqual()
    {
        // arrange - bytes representing int16 min value
        var expectedBytes = new byte[] { 0x0, 0x80 };

        // arrange - calculate int16 min value
        const int int16Bits = 16;
        var int16Min = (short)(Math.Pow(2, int16Bits) / 2 * -1);

        // act - convert int32 value to bytes
        var bytes = new byte[2];
        LittleEndianConverter.ConvertInt16ToBytes(int16Min, bytes);

        // assert - bytes are is equal to expected int16 min value bytes
        Assert.Equal(expectedBytes, bytes);
    }
}