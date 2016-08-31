﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using NetSim.Lib.Simulator;
// ReSharper disable UnusedMember.Local

namespace NetSim.Lib.Routing.DSR
{
    public class DsrRoutingProtocol : NetSimRoutingProtocol
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="DsrRoutingProtocol"/> class.
        /// </summary>
        /// <param name="client">The client.</param>
        public DsrRoutingProtocol(NetSimClient client) : base(client) { }

        /// <summary>
        /// Gets the output message queue (should be used only for data messages).
        /// </summary>
        /// <value>
        /// The output queue.
        /// </value>
        public Queue<NetSimQueuedMessage> OutputQueue { get; private set; }

        /// <summary>
        /// Gets the request cache.
        /// </summary>
        /// <value>
        /// The request cache.
        /// </value>
        public List<DsrCacheEntry> RequestCache { get; private set; }

        /// <summary>
        /// Gets or sets the current request identifier.
        /// </summary>
        /// <value>
        /// The current request identifier.
        /// </value>
        public int CurrentRequestId { get; set; }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public override void Initialize()
        {
            // call base initialization (stepcounter and data)
            base.Initialize();

            //intialize routing table
            this.Table = new DsrTable();

            //intialize request cache
            this.RequestCache = new List<DsrCacheEntry>();

            // intialize request id for route request identification 
            this.CurrentRequestId = 1;

            //intialize outgoing messages
            this.OutputQueue = new Queue<NetSimQueuedMessage>();

            // local table reference casted to the right type
            var localTableRef = (DsrTable)this.Table;

            // self routing entry with metric 0
            localTableRef.AddInitialRouteEntry(Client.Id, Client.Id, 0);
        }

        /// <summary>
        /// Performs the routing step.
        /// </summary>
        public override void PerformRoutingStep()
        {
            //handle all incomming messages
            HandleIncommingMessages();

            //handle outgoing queued messages
            HandleOutgoingMessages();

            stepCounter++;
        }

        /// <summary>
        /// Sends the message.
        /// </summary>
        /// <param name="message">The message.</param>
        public override void SendMessage(NetSimMessage message)
        {
            // queue message
            OutputQueue.Enqueue(new NetSimQueuedMessage()
            {
                Message = message,
            });
        }

        /// <summary>
        /// Handles the outgoing messages.
        /// </summary>
        private void HandleOutgoingMessages()
        {
            // get the count of queued messages 
            int counter = OutputQueue.Count;

            // run for each queued message
            while (counter > 0)
            {
                // get next queued message
                var queuedMessage = OutputQueue.Dequeue();

                //check if message is a dsr message
                if (IsDsrMessage(queuedMessage.Message))
                {
                    // handle "resend" of dsr message intialy send from other node
                    ForwardDsrMessage(queuedMessage);
                }
                else
                {
                    //if here messages gets initally send from this node
                    HandleRouteDiscoveryForOutgoingMessage(queuedMessage);
                }
                counter--;
            }
        }

        /// <summary>
        /// Handles the new outgoing message.
        /// </summary>
        /// <param name="queuedMessage">The queued message.</param>
        private void HandleRouteDiscoveryForOutgoingMessage(NetSimQueuedMessage queuedMessage)
        {
            //search the route for message (also in cache)
            var searchedRoute = GetDsrRoute(queuedMessage.Message.Receiver);

            // if route found - send the message via the connection
            if (searchedRoute != null)
            {
                var sendMessage = queuedMessage.Message;

                //pack message in dsrframemessage and set found route
                var dsrFrame = new DsrFrameMessage()
                {
                    Data = (NetSimMessage)sendMessage.Clone(),
                    Receiver = sendMessage.Receiver,
                    Sender = sendMessage.Sender,
                    Route = new List<string>(searchedRoute.Route),
                };

                //determine the next hop of the requested route
                var nextHopId = dsrFrame.GetNextHop(Client.Id);

                //lay message on wire - intial send
                Client.Connections[nextHopId].StartTransportMessage(queuedMessage.Message, nextHopId);
            }
            else
            {
                // if route not found and route discovery is not started for this message
                if (!queuedMessage.IsRouteDiscoveryStarted)
                {
                    // mark as started
                    queuedMessage.IsRouteDiscoveryStarted = true;

                    //broadcast to all neighbors
                    Client.BroadcastMessage(new DsrRouteRequestMessage()
                    {
                        Sender = Client.Id,
                        Receiver = queuedMessage.Message.Receiver,
                        RequestId = this.CurrentRequestId,
                        Nodes = { this.Client.Id }
                    }, false);

                    //increase route request id
                    this.CurrentRequestId++;
                }

                // and enqueue message again
                OutputQueue.Enqueue(queuedMessage);
            }
        }

        /// <summary>
        /// Handles the outgoing DSR message.
        /// Note: Messages handled in this methods where intialy send from another node.
        /// </summary>
        /// <param name="queuedMessage">The queued message.</param>
        private void ForwardDsrMessage(NetSimQueuedMessage queuedMessage)
        {
            // searches a handler method with the dsrmessagehandler attribute and the 
            // right message type and for incoming(false) or outgoing (true, default) messages.
            // e.g. IncomingDsrRouteRequestMessageHandler
            var method = GetHandlerMethod(queuedMessage.Message.GetType());

            method?.Invoke(this, new object[] { queuedMessage });
        }

        /// <summary>
        /// Gets the handler method.
        /// searches a handler method with the dsrmessagehandler attribute and the 
        /// right message type and for incoming(false) or outgoing (true, default) messages.
        /// e.g. IncomingDsrRouteRequestMessageHandler
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="searchOutgoing">if set to <c>true</c> [search outgoing].</param>
        /// <returns></returns>
        private MethodInfo GetHandlerMethod(Type messageType, bool searchOutgoing = true)
        {
            var method =
                this.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(m =>
                    {
                        DsrMessageHandlerAttribute attribute =
                            m.GetCustomAttributes()
                                .FirstOrDefault(c => c.GetType() == typeof(DsrMessageHandlerAttribute)) as
                            DsrMessageHandlerAttribute;
                        return attribute != null && attribute.MessageToHandle == messageType && attribute.Outgoing == searchOutgoing;
                    });
            return method;
        }

        /// <summary>
        /// The outgoing DSR frame message handler.
        /// dsrframe - forward to destination
        /// </summary>
        /// <param name="queuedMessage">The queued message.</param>
        [DsrMessageHandler(typeof(DsrFrameMessage), Outgoing = true)]
        private void OutgoingDsrFrameMessageHandler(NetSimQueuedMessage queuedMessage)
        {
            //get dsr frame message instacne
            var frameMessage = (DsrFrameMessage)queuedMessage.Message;

            // get next hop from in frame message saved route info
            var nextHop = frameMessage.GetNextHop(Client.Id);

            // lay message "on" wire - start transmitting via connection
            Client.Connections[nextHop].StartTransportMessage(frameMessage, nextHop);
        }

        /// <summary>
        /// Handles the outgoing dsr frame message.
        /// dsrrouterequest - send to every neighbor
        /// </summary>
        /// <param name="queuedMessage">The queued message.</param>
        [DsrMessageHandler(typeof(DsrRouteRequestMessage), Outgoing = true)]
        private void OutgoingDsrRouteRequestMessageHandler(NetSimQueuedMessage queuedMessage)
        {
            //get dsr requestMessage instacne - note: the request message was alreay handled in incomming messages
            var requestMessage = (DsrRouteRequestMessage)queuedMessage.Message;

            //broadcast to all neighbors
            Client.BroadcastMessage(requestMessage, false);
        }

        /// <summary>
        /// Handles the outgoing DsrRouteResponseMessage.
        /// dsrrouterespone - forward the reverse route way
        /// </summary>
        /// <param name="queuedMessage">The queued message.</param>
        [DsrMessageHandler(typeof(DsrRouteResponseMessage), Outgoing = true)]
        private void OutgoingDsrRouteResponseMessageHandler(NetSimQueuedMessage queuedMessage)
        {
            var responseMessage = (DsrRouteResponseMessage)queuedMessage.Message;

            // get the next hop id from the route info saved within this message
            string nextHopId = responseMessage.GetNextReverseHop(Client.Id);

            // start message transport
            Client.Connections[nextHopId].StartTransportMessage(responseMessage, nextHopId);
        }

        /// <summary>
        ///Handles the outgoing DSR remove route messages.
        /// </summary>
        /// <param name="queuedMessage">The queued message.</param>
        [DsrMessageHandler(typeof(DsrRouteRemoveMessage), Outgoing = true)]
        private void OutgoingDsrRouteRemoveMessageHandler(NetSimQueuedMessage queuedMessage)
        {
            var removeMessage = (DsrRouteRemoveMessage)queuedMessage.Message;

            throw new NotImplementedException();
        }

        /// <summary>
        /// Handles the incomming messages.
        /// </summary>
        private void HandleIncommingMessages()
        {
            if (Client.InputQueue.Count <= 0)
            {
                return;
            }

            while (Client.InputQueue.Count > 0)
            {
                // dequues message to handle
                var message = Client.InputQueue.Dequeue();

                // searches a handler method with the dsrmessagehandler attribute and the 
                // right message type and for incoming(false) or outgoing (true) messages.
                // e.g. IncomingDsrRouteRequestMessageHandler
                var method = GetHandlerMethod(message.GetType(), false);

                //call handler
                method?.Invoke(this, new object[] { message });

                // ignore all not dsr messages - all messages have to be packed in dsrFrame                
            }
        }

        /// <summary>
        /// Handles the incomming dsr route request message.
        /// </summary>
        /// <param name="message">The message.</param>
        [DsrMessageHandler(typeof(DsrRouteRequestMessage), Outgoing = false)]
        private void IncomingDsrRouteRequestMessageHandler(NetSimMessage message)
        {
            DsrRouteRequestMessage reqMessage = (DsrRouteRequestMessage)message;

            //if duplicate
            if (HasCachedRequest(reqMessage))
            {
                //ignore message and proceed
                return;
            }

            //add request to cache
            AddCachedRequest(reqMessage);

            //add this node id to message Route
            reqMessage.Nodes.Add(this.Client.Id);

            //if this node was sender of request - ignore
            if(IsOwnRequest(reqMessage))
            {
                return;
            }

            //check if message destination is current node (me)
            if (reqMessage.Receiver.Equals(this.Client.Id))
            {
                //send back rrep mesage the reverse way with found route 
                var response = new DsrRouteResponseMessage()
                {
                    Receiver = reqMessage.Sender,
                    Sender = Client.Id,
                    Route = new List<string>(reqMessage.Nodes)
                };

                //enqueue message for sending
                SendMessage(response);

                return;
            }

            // forward message to outgoing messages
            SendMessage(reqMessage);
        }

        /// <summary>
        /// Determines whether the given dsr rreq message is a own request.
        /// </summary>
        /// <param name="reqMessage">The req message.</param>
        /// <returns>
        ///   <c>true</c> if is own request; otherwise, <c>false</c>.
        /// </returns>
        private bool IsOwnRequest(DsrRouteRequestMessage reqMessage)
        {
            return reqMessage.Nodes.Count > 0 && reqMessage.Nodes[0].Equals(this.Client.Id);
        }

        /// <summary>
        /// Handles the  Incomings the DSR route response message.
        /// </summary>
        /// <param name="message">The message.</param>
        [DsrMessageHandler(typeof(DsrRouteResponseMessage), Outgoing = false)]
        private void IncomingDsrRouteResponseMessageHandler(NetSimMessage message)
        {
            DsrRouteResponseMessage repMessage = (DsrRouteResponseMessage)message;

            //check if the respone is for this node
            if (repMessage.Receiver.Equals(Client.Id))
            {
                //save found route to cache

                return;
            }

            // forward message
            SendMessage(repMessage);
        }

        /// <summary>
        /// Handles the  Incommings the DSR route remove message.
        /// </summary>
        /// <param name="message">The message.</param>
        [DsrMessageHandler(typeof(DsrRouteRemoveMessage), Outgoing = false)]
        private void IncomingDsrRouteRemoveMessageHandler(NetSimMessage message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Handles the  Incommings the DSR frame message.
        /// </summary>
        /// <param name="message">The message.</param>
        [DsrMessageHandler(typeof(DsrFrameMessage), Outgoing = false)]
        private void IncomingDsrFrameMessageHandler(NetSimMessage message)
        {
            // forward message if client is not reciever
            if (!message.Receiver.Equals(this.Client.Id))
            {
                SendMessage(message);
            }

            //unpack mesage from dsrframe
            var dsrFrame = (DsrFrameMessage)message;

            //forward message to client
            Client.ReceiveData(dsrFrame.Data);
        }


        /// <summary>
        /// Determines whether if message type is a DSR message type.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        private bool IsDsrMessage(NetSimMessage message)
        {
            List<Type> dsrTypes = new List<Type>()
            {
                typeof(DsrRouteResponseMessage),
                typeof(DsrRouteRequestMessage),
                typeof(DsrRouteRemoveMessage),
                typeof(DsrFrameMessage)
            };

            Type messageType = message.GetType();
            return dsrTypes.Any(t => t == messageType);
        }

        /// <summary>
        /// Gets the DSR cached route.
        /// </summary>
        /// <param name="receiver">The receiver.</param>
        /// <returns></returns>
        private DsrTableEntry GetDsrRoute(string receiver)
        {
            return (DsrTableEntry)Table.GetRouteFor(receiver);
        }

        /// <summary>
        /// Adds the cached request.
        /// </summary>
        /// <param name="reqMessage">The req message.</param>
        private void AddCachedRequest(DsrRouteRequestMessage reqMessage)
        {
            var nodeCache = RequestCache.FirstOrDefault(r => r.Id.Equals(reqMessage.Sender));

            if (nodeCache == null)
            {
                nodeCache = new DsrCacheEntry() { Id = reqMessage.Sender };

                RequestCache.Add(nodeCache);
            }

            nodeCache.ChachedRequests.Add(reqMessage.RequestId);
        }

        /// <summary>
        /// Determines whether this protocol instance has chached request the specified reqid.
        /// </summary>
        /// <param name="reqMessaged">The req messaged.</param>
        /// <returns></returns>
        private bool HasCachedRequest(DsrRouteRequestMessage reqMessaged)
        {
            return
                RequestCache
                .FirstOrDefault(r => r.Id.Equals(reqMessaged.Sender))?
                    .HasCachedRequest(reqMessaged.RequestId) ?? false;
        }

        protected override string GetRoutingData()
        {
            //TODO
            return Table.ToString();
        }
    }
}
