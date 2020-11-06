using System.Linq;
using System.Net;
using System.Net.Sockets;
using STUN.Attributes;

namespace STUN
{
    public class STUNRfc5780
    {
        public static STUNQueryResult Query(Socket socket, IPEndPoint server, STUNQueryType queryType, int ReceiveTimeout)
        {
            STUNNatMappingBehavior mappingBehavior = STUNNatMappingBehavior.EndpointIndependentMapping;
            STUNNatFilteringBehavior filteringBehavior = STUNNatFilteringBehavior.EndpointIndependentFiltering;
            var result = new STUNQueryResult(); // the query result
            result.Socket = socket;
            result.ServerEndPoint = server;
            result.NATType = STUNNATType.Unspecified;
            result.QueryType = queryType;

            var transID = STUNMessage.GenerateTransactionIDNewStun(); // get a random trans id
            var message = new STUNMessage(STUNMessageTypes.BindingRequest, transID); // create a bind request
            // send the request to server
            socket.SendTo(message.GetBytes(), server);
            // we set result local endpoint after calling SendTo,
            // because if socket is unbound, the system will bind it after SendTo call.
            result.LocalEndPoint = socket.LocalEndPoint as IPEndPoint;

            // wait for response
            var responseBuffer = STUNUtils.Receive(socket, ReceiveTimeout);

            // didn't receive anything
            if (responseBuffer == null)
            {
                throw new StunRequestTimeout();
            }

            // try to parse message
            if (!message.TryParse(responseBuffer))
            {
                throw new StunBadResponse();
            }

            // check trans id
            if (!STUNUtils.ByteArrayCompare(message.TransactionID, transID))
            {
                throw new StunBadTransactionId();
            }

            // finds error-code attribute, used in case of binding error
            var errorAttr = message.Attributes.FirstOrDefault(p => p is STUNErrorCodeAttribute)
                as STUNErrorCodeAttribute;

            // if server responsed our request with error
            if (message.MessageType == STUNMessageTypes.BindingErrorResponse)
            {
                if (errorAttr == null)
                {
                    // we count a binding error without error-code attribute as bad response (no?)
                    throw new StunBadResponse();
                }

                throw new StunServerError(errorAttr.Error, errorAttr.Phrase);
            }

            // return if receive something else binding response
            if (message.MessageType != STUNMessageTypes.BindingResponse)
            {
                throw new StunBadResponse();
            }

            var xorAddressAttribute = message.Attributes.FirstOrDefault(p => p is STUNXorMappedAddressAttribute)
                as STUNXorMappedAddressAttribute;

            if (xorAddressAttribute == null)
            {
                throw new StunBadResponse();
            }

            result.PublicEndPoint = xorAddressAttribute.EndPoint;

            // stop querying and return the public ip if user just wanted to know public ip
            if (queryType == STUNQueryType.PublicIP)
            {
                return result;
            }


            if (xorAddressAttribute.EndPoint.Equals(socket.LocalEndPoint))
            {
                result.NATType = STUNNATType.OpenInternet;
            }

            var otherAddressAttribute = message.Attributes.FirstOrDefault(p => p is STUNOtherAddressAttribute)
                as STUNOtherAddressAttribute;

            var changedAddressAttribute = message.Attributes.FirstOrDefault(p => p is STUNChangedAddressAttribute)
                as STUNChangedAddressAttribute;
            // Check is next test should be performed and is support rfc5780 test
            if (otherAddressAttribute == null)
            {
                if (changedAddressAttribute == null)
                {
                    throw new StunUnsupportedRequest();
                }

                otherAddressAttribute = new STUNOtherAddressAttribute();
                otherAddressAttribute.EndPoint = changedAddressAttribute.EndPoint;
            }


            // Make test 2 - bind different ip address but primary port
            message = new STUNMessage(STUNMessageTypes.BindingRequest, transID); // create a bind request
            IPEndPoint secondaryServer = new IPEndPoint(otherAddressAttribute.EndPoint.Address, server.Port);
            socket.SendTo(message.GetBytes(), secondaryServer);
            responseBuffer = STUNUtils.Receive(socket, ReceiveTimeout);

            // Secondary server presented but is down
            if (responseBuffer == null)
            {
                throw new StunUnsupportedRequest();
            }

            if (!message.TryParse(responseBuffer))
            {
                throw new StunUnsupportedRequest();
            }

            var xorAddressAttribute2 = message.Attributes.FirstOrDefault(p => p is STUNXorMappedAddressAttribute)
                as STUNXorMappedAddressAttribute;

            if (xorAddressAttribute2 != null)
            {
                if (xorAddressAttribute.EndPoint.Equals(xorAddressAttribute2.EndPoint))
                {
                    mappingBehavior = STUNNatMappingBehavior.EndpointIndependentMapping;
                }

                // Make test 3
                else
                {
                    IPEndPoint secondaryServerPort = new IPEndPoint(otherAddressAttribute.EndPoint.Address,
                        otherAddressAttribute.EndPoint.Port);

                    message = new STUNMessage(STUNMessageTypes.BindingRequest, transID); // create a bind request
                    socket.SendTo(message.GetBytes(), secondaryServerPort);
                    responseBuffer = STUNUtils.Receive(socket, ReceiveTimeout);

                    if (!message.TryParse(responseBuffer))
                    {
                        throw new StunUnsupportedRequest();
                    }

                    var xorAddressAttribute3 =
                        message.Attributes.FirstOrDefault(p => p is STUNXorMappedAddressAttribute)
                            as STUNXorMappedAddressAttribute;

                    if (xorAddressAttribute3 != null)
                    {
                        if (xorAddressAttribute3.EndPoint.Equals(xorAddressAttribute2.EndPoint))
                        {
                            mappingBehavior = STUNNatMappingBehavior.AddressDependMapping;
                        }

                        else
                        {
                            mappingBehavior = STUNNatMappingBehavior.AddressAndPortDependMapping;
                        }
                    }
                }
            }

            // Now make a filtering behavioral test
            // We already made a test 1 for mapping behavioral
            // so jump to test 2

            // Send message to primary server.
            // Try receive from another server and port
            message = new STUNMessage(STUNMessageTypes.BindingRequest, transID);
            message.Attributes.Add(new STUNChangeRequestAttribute(true, true));

            socket.SendTo(message.GetBytes(), server);

            responseBuffer = STUNUtils.Receive(socket, ReceiveTimeout);

            if (responseBuffer != null)
            {
                filteringBehavior = STUNNatFilteringBehavior.EndpointIndependentFiltering;
            }

            // Test 3 - send request to original server with change port attribute
            else
            {
                message = new STUNMessage(STUNMessageTypes.BindingRequest, transID);
                message.Attributes.Add(new STUNChangeRequestAttribute(false, true));

                socket.SendTo(message.GetBytes(), server);

                responseBuffer = STUNUtils.Receive(socket, ReceiveTimeout);

                if (responseBuffer != null)
                {
                    filteringBehavior = STUNNatFilteringBehavior.AddressDependFiltering;
                }

                else
                {
                    filteringBehavior = STUNNatFilteringBehavior.AddressAndPortDependFiltering;
                }
            }

            if (filteringBehavior == STUNNatFilteringBehavior.AddressAndPortDependFiltering &&
                mappingBehavior == STUNNatMappingBehavior.AddressAndPortDependMapping)
            {
                result.NATType = STUNNATType.Symmetric;
            }

            if (filteringBehavior == STUNNatFilteringBehavior.EndpointIndependentFiltering &&
                mappingBehavior == STUNNatMappingBehavior.EndpointIndependentMapping)
            {
                result.NATType = STUNNATType.FullCone;
            }

            if (filteringBehavior == STUNNatFilteringBehavior.EndpointIndependentFiltering &&
                mappingBehavior == STUNNatMappingBehavior.AddressDependMapping)
            {
                result.NATType = STUNNATType.Restricted;
            }

            if (result.NATType == STUNNATType.Unspecified)
            {
                result.NATType = STUNNATType.PortRestricted;
            }

            return result;
        }
    }
}