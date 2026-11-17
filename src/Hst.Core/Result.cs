namespace Hst.Core
{
    using System;

    /// <summary>
    /// Result of an operation, which is either successful or faulted with an error.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Create successful result.
        /// </summary>
        public Result()
        {
            Error = null;
        }

        /// <summary>
        /// Create faulted result with error.
        /// </summary>
        /// <param name="error">Error describing why operation failed.</param>
        public Result(Error error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        /// <summary>
        /// Error, if result is faulted. Otherwise null.
        /// </summary>
        public Error Error { get; }

        /// <summary>
        /// Result is successful.
        /// </summary>
        public bool IsSuccess => Error == null;

        /// <summary>
        /// Result is faulted with an error.
        /// </summary>
        public bool IsFaulted => Error != null;
    }

    /// <summary>
    /// Result of an operation, which is either successful with a value or faulted with an error.
    /// </summary>
    /// <typeparam name="T">Type of value.</typeparam>
    public class Result<T> : Result
    {
        /// <summary>
        /// Create successful result with value.
        /// </summary>
        /// <param name="value">Value of result.</param>
        public Result(T value)
        {
            Value = value;
        }

        /// <summary>
        /// Create faulted result with error.
        /// </summary>
        /// <param name="error">Error describing why operation failed.</param>
        public Result(Error error)
            : base(error)
        {
            Value = default;
        }

        /// <summary>
        /// Value, if result is successful. Otherwise default value of type.
        /// </summary>
        public T Value { get; }
    }
}
