using System;

namespace STUN
{
    public class STUNException : Exception
    {
        public STUNException(STUNQueryError queryError, STUNErrorCodes serverError, string serverErrorPhrase)
            : base($"Stun Erorr: Error {queryError} {serverError} {serverErrorPhrase}")
        {
        }
    }
}