using System;

namespace STUN
{
    public class StunException : Exception
    {
        public StunException() { }
        public StunException(string message) : base("Stun Error: " + message) { }
    }

    public class StunServerError : StunException
    {
        public StunServerError(STUNErrorCodes error, string phrase) : base($"Server error (Error: {error}, Phrase: {phrase})") { }
    }

    public class StunBadResponse : StunException
    {
        public StunBadResponse() : base("Bad response") { }
    }

    public class StunBadTransactionId : StunException
    {
        public StunBadTransactionId() : base("Bad transaction id") { }
    }

    public class StunRequestTimeout : StunException
    {
        public StunRequestTimeout() : base("Request timed out") { }
    }

    public class StunUnsupportedRequest : StunException
    {
        public StunUnsupportedRequest() : base("Request Not Supported") { }
    }

}
