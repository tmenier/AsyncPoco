using System;
using System.Runtime.Serialization;

namespace AsyncPoco.Exceptions
{
    public class UninitializedPrimaryKeyException : SystemException
    {
        public UninitializedPrimaryKeyException()
        {
        }

        public UninitializedPrimaryKeyException(string message) : base(message)
        {
        }

        public UninitializedPrimaryKeyException(string message, Exception inner) : base(message, inner)
        {
        }

        // This constructor is needed for serialization.
        protected UninitializedPrimaryKeyException(SerializationInfo info, StreamingContext context)
        {
        }

        public static UninitializedPrimaryKeyException showKeyMessage(dynamic key)
        {
            string message = message = "Primary key, " + key + ", is not initialized to an incremental value.";

            return new UninitializedPrimaryKeyException(message);
        }
    }
}
