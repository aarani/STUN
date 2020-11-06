using System;

namespace STUN
{
    internal class STUNException : Exception
    {
        public STUNException(STUNQueryError queryError, STUNErrorCodes serverError, string serverErrorPhrase)
            : base($"Stun Erorr: Error {queryError} {serverError} {serverErrorPhrase}")
        {
        }
    }
}