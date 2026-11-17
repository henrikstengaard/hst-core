namespace Hst.Core.Tests.ResultTests
{
    using System;
    using Xunit;

    public class GivenResult
    {
        [Fact]
        public void WhenCreatingSuccessfulResultThenResultIsSuccess()
        {
            var result = new Result();

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFaulted);
            Assert.Null(result.Error);
        }

        [Fact]
        public void WhenCreatingFaultedResultThenResultIsFaultedWithError()
        {
            var error = new Error("failed");

            var result = new Result(error);

            Assert.False(result.IsSuccess);
            Assert.True(result.IsFaulted);
            Assert.Same(error, result.Error);
            Assert.Equal("failed", result.Error.Message);
        }

        [Fact]
        public void WhenCreatingFaultedResultWithNullErrorThenExceptionIsThrown()
        {
            Assert.Throws<ArgumentNullException>(() => new Result(null));
        }

        [Fact]
        public void WhenCreatingSuccessfulResultWithValueThenValueIsReturned()
        {
            var result = new Result<int>(42);

            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void WhenCreatingSuccessfulResultWithNullValueThenResultIsSuccess()
        {
            var result = new Result<string>((string)null);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value);
        }

        [Fact]
        public void WhenCreatingFaultedResultWithValueTypeThenValueIsDefault()
        {
            var result = new Result<int>(new Error("failed"));

            Assert.True(result.IsFaulted);
            Assert.Equal(0, result.Value);
        }
    }
}
