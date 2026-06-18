using System;
using Hst.Core.Converters;
using Xunit;

namespace Hst.Core.Tests.ConverterTests;

public class GivenLittleEndianConverterWithUInt16Values
{
    [Fact]
    public void When_ConvertBytesRepresentingUInt16MinValue_Then_ValueIsEqualToUInt16MinValue()
    {
        // arrange - uint16 min value
        ushort uint16Min = 0;

        // arrange - bytes representing uint16 min value
        var bytes = new byte[] { 0x0, 0x0 };

        // act - convert bytes to uint16 value
        var uint16Value = LittleEndianConverter.ConvertBytesToUInt16(bytes);

        // assert - uint16 value is equal to min uint16
        Assert.Equal(uint16Min, uint16Value);
        Assert.Equal(ushort.MinValue, uint16Value);
    }
    
    [Fact]
    public void When_ConvertBytesRepresentingUInt16MaxValue_Then_ValueIsEqualToUInt16MaxValue()
    {
        // arrange - calculate uint16 max value
        var int16Bits = 16;
        var uint16Max = Math.Pow(2, int16Bits) - 1;

        // arrange - bytes representing uint16 max value
        var bytes = new byte[] { 0xff, 0xff };

        // act - convert bytes to uint16 value
        var int16Value = LittleEndianConverter.ConvertBytesToUInt16(bytes);

        // assert - uint16 value is equal to max uint16
        Assert.Equal(uint16Max, int16Value);
        Assert.Equal(UInt16.MaxValue, int16Value);
    }
    
    [Fact]
    public void When_ConvertBytesRepresentingUInt16Value511_Then_ValueIsEqual()
    {
        // arrange - bytes representing uint16 value 511
        var bytes = new byte[] { 0xff, 0x1 };

        // act - convert bytes to uint16 value
        var uint16Value = LittleEndianConverter.ConvertBytesToUInt16(bytes);

        // assert - uint16 value is equal to 511
        Assert.Equal(256U + 255, uint16Value);
    }
    
    [Fact]
    public void When_ConvertUInt16Value511_Then_BytesAreEqual()
    {
        // arrange - bytes representing uint16 value 511
        var expectedBytes = new byte[]{ 0xff, 0x1 };
            
        // act - convert uint16 value to bytes
        var bytes = new byte[2];
        LittleEndianConverter.ConvertUInt16ToBytes(511, bytes);

        // assert - uint16 value is equal to 511
        Assert.Equal(expectedBytes, bytes);
    }
        
    [Fact]
    public void When_ConvertUInt16MaxValue_Then_BytesAreEqual()
    {
        // arrange - bytes representing uint16 max value
        var expectedBytes = new byte[] { 0xff, 0xff };

        // arrange - calculate uint16 max value
        const int uInt16Bits = 16;
        var uInt16Max = (ushort)(Math.Pow(2, uInt16Bits) - 1);
            
        // act - convert uint16 value to bytes
        var bytes = new byte[2];
        LittleEndianConverter.ConvertUInt16ToBytes(uInt16Max, bytes);

        // assert - bytes are is equal to expected uint16 max value bytes
        Assert.Equal(expectedBytes, bytes);
    }
        
    [Fact]
    public void When_ConvertUInt16MinValue_Then_BytesAreEqual()
    {
        // arrange - bytes representing uint16 min value
        var expectedBytes = new byte[] { 0x0, 0x0 };

        // arrange - calculate uint16 min value
        const ushort uInt16Min = 0;

        // act - convert uint16 value to bytes
        var bytes = new byte[2];
        LittleEndianConverter.ConvertUInt16ToBytes(uInt16Min, bytes);

        // assert - bytes are is equal to expected uint16 min value bytes
        Assert.Equal(expectedBytes, bytes);
    }
}