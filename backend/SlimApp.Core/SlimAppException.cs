using System;
using System.Runtime.Serialization;

namespace SlimApp
{
    /// <summary>
    /// Base exception type for those are thrown by SlimApp system for SlimApp specific exceptions.
    /// </summary>
    [Serializable]
    public class SlimAppException : Exception
    {
        /// <summary>
        /// Creates a new <see cref="SlimAppException"/> object.
        /// </summary>
        public SlimAppException()
        {

        }

        /// <summary>
        /// Creates a new <see cref="SlimAppException"/> object.
        /// </summary>
        public SlimAppException(SerializationInfo serializationInfo, StreamingContext context)
            : base(serializationInfo, context)
        {

        }

        /// <summary>
        /// Creates a new <see cref="SlimAppException"/> object.
        /// </summary>
        /// <param name="message">Exception message</param>
        public SlimAppException(string message)
            : base(message)
        {

        }

        /// <summary>
        /// Creates a new <see cref="SlimAppException"/> object.
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public SlimAppException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}
