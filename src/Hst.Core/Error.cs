namespace Hst.Core
{
    /// <summary>
    /// Error describing why an operation failed.
    /// </summary>
    public class Error
    {
        /// <summary>
        /// Message describing error.
        /// </summary>
        public string Message { get; }

        public Error()
        {
            Message = "Error";
        }

        public Error(string message)
        {
            Message = message;
        }

        public override string ToString()
        {
            return Message;
        }
    }
}
