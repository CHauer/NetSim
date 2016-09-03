﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Documents;

using NetSim.Lib.Simulator;
using NetSim.Lib.Simulator.Components;

namespace NetSim.Lib.Routing.DSR
{
    public class DsrTable : NetSimTable
    {
        /// <summary>
        /// Adds the initial route entry.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <param name="nextHop">The next hop.</param>
        /// <param name="metric">The metric.</param>
        public void AddInitialRouteEntry(string destination, string nextHop, int metric)
        {
            this.Entries.Add(new DsrTableEntry()
            {
                Destination = destination,
                Metric = metric,
                Route = new List<string>() { nextHop }
            });
        }

        /// <summary>
        /// Handles the response.
        /// </summary>
        /// <param name="message">The message.</param>
        public void HandleResponse(DsrRouteReplyMessage message)
        {
            // search for a route for this destination
            var entry = GetRouteFor(message.Sender);

            // if no route found or the metric of the found route is bigger
            if (entry == null || entry.Metric > message.Route.Count)
            {
                this.Entries.Add(new DsrTableEntry()
                {
                    Destination = message.Sender,
                    Metric = message.Route.Count,
                    Route = new List<string>(message.Route)
                });
            }
        }

        /// <summary>
        /// Handles the response.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="notReachableNode">The not reachable node.</param>
        public void HandleError(string sender, string notReachableNode)
        {
            // search entries where message sender and message notreachable are in path (direct connected)
            foreach (var netSimTableEntry in Entries.Where(e => ((DsrTableEntry)e).Route.Contains(notReachableNode)).ToList())
            {
                var entry = (DsrTableEntry)netSimTableEntry;

                int index = entry.Route.IndexOf(notReachableNode);

                if (index - 1 < 0)
                    continue;

                if (entry.Route[index - 1].Equals(sender))
                {
                    RemoveRoute(entry.Destination);
                }
            }
        }

        /// <summary>
        /// Handles the request route caching.
        /// </summary>
        /// <param name="reqMessage">The req message.</param>
        public void HandleRequestRouteCaching(DsrRouteRequestMessage reqMessage)
        {
            // search for already cached route
            var route = (DsrTableEntry)this.GetRouteFor(reqMessage.Sender);

            // reverse the request route
            var cachedRoute = reqMessage.Nodes.Reverse<string>().ToList();

            if (route == null)
            {
                // add route to table
                Entries.Add(new DsrTableEntry()
                {
                    Destination = reqMessage.Sender,
                    Route = cachedRoute,
                    Metric = cachedRoute.Count
                });
            }
            else
            {
                // check if new route is shorter
                if (cachedRoute.Count < route.Metric)
                {
                    // remove route and add new one
                    Entries.Remove(route);

                    // add new route
                    Entries.Add(new DsrTableEntry()
                    {
                        Destination = reqMessage.Sender,
                        Route = cachedRoute,
                        Metric = cachedRoute.Count
                    });
                }
            }
        }

        /// <summary>
        /// Handles the request route caching.
        /// </summary>
        /// <param name="repMessage">The rep message.</param>
        /// <param name="clientId">The client identifier.</param>
        public void HandleReplyRouteCaching(DsrRouteReplyMessage repMessage, string clientId)
        {
            // search for already cached route
            var route = (DsrTableEntry)this.GetRouteFor(repMessage.Sender);

            // skip the route until the own id is found in it - the rest is the path to the sender
            var cachedRoute = repMessage.Route.SkipWhile(e => !e.Equals(clientId)).ToList();

            // if route is empty drop
            if (cachedRoute.Count == 0)
            {
                return;
            }

            if (route == null)
            {
                // add route to table
                Entries.Add(new DsrTableEntry()
                {
                    Destination = repMessage.Sender,
                    Route = cachedRoute,
                    Metric = cachedRoute.Count
                });
            }
            else
            {
                // check if new route is shorter
                if (cachedRoute.Count < route.Metric)
                {
                    // remove route and add new one
                    Entries.Remove(route);

                    // add new route
                    Entries.Add(new DsrTableEntry()
                    {
                        Destination = repMessage.Sender,
                        Route = cachedRoute,
                        Metric = cachedRoute.Count
                    });
                }
            }
        }

        /// <summary>
        /// Removes the route.
        /// </summary>
        /// <param name="destination">The destination.</param>
        private void RemoveRoute(string destination)
        {
            var entry = GetRouteFor(destination);

            if (entry != null)
            {
                this.Entries.Remove(entry);
            }
        }

        /// <summary>
        /// Gets the route for.
        /// </summary>
        /// <param name="destinationId">The destination identifier.</param>
        /// <returns></returns>
        public override NetSimTableEntry GetRouteFor(string destinationId)
        {
            return Entries.FirstOrDefault(e => e.Destination.Equals(destinationId));
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override object Clone()
        {
            return new DsrTable() { Entries = this.Entries.Select(e => (NetSimTableEntry)e.Clone()).ToList() };
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine($"Dest Metric Route");
            builder.Append(base.ToString());

            return builder.ToString();
        }
    }
}