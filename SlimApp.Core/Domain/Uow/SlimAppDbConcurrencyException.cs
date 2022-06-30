using System;
using System.Runtime.Serialization;

namespace SlimApp.Domain.Uow
{
    [Serializable]
    public class SlimAppDbConcurrencyException : SlimAppException
    {
        /// <summary>
        /// Creates a new <see cref="SlimAppDbConcurrencyException"/> object.
        /// </summary>
        public SlimAppDbConcurrencyException()
        {

        }

        /// <summary>
        /// Creates a new <see cref="SlimAppException"/> object.
        /// </summary>
        public SlimAppDbConcurrencyException(SerializationInfo serializationInfo, StreamingContext context)
            : base(serializationInfo, context)
        {

        }

        /// <summary>
        /// Creates a new <see cref="SlimAppDbConcurrencyException"/> object.
        /// </summary>
        /// <param name="message">Exception message</param>
        public SlimAppDbConcurrencyException(string message)
            : base(message)
        {

        }

        /// <summary>
        /// Creates a new <see cref="SlimAppDbConcurrencyException"/> object.
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public SlimAppDbConcurrencyException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}