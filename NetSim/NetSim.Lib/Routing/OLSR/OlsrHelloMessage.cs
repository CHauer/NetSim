﻿// -----------------------------------------------------------------------
// <copyright file="OlsrHelloMessage.cs" company="FH Wr.Neustadt">
//      Copyright Christoph Hauer. All rights reserved.
// </copyright>
// <author>Christoph Hauer</author>
// <summary>NetSim.Lib - OlsrHelloMessage.cs</summary>
// -----------------------------------------------------------------------

namespace NetSim.Lib.Routing.OLSR
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using NetSim.Lib.Simulator.Components;

    /// <summary>
    /// The olsr hello message implementation.
    /// </summary>
    /// <seealso cref="NetSim.Lib.Simulator.Components.NetSimMessage" />
    public class OlsrHelloMessage : NetSimMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OlsrHelloMessage"/> class.
        /// </summary>
        public OlsrHelloMessage()
        {
            this.Neighbors = new List<string>();
            this.MultiPointRelays = new List<string>();
        }

        /// <summary>
        /// Gets or sets the multi point relays.
        /// </summary>
        /// <value>
        /// The multi point relays.
        /// </value>
        public List<string> MultiPointRelays { get; set; }

        /// <summary>
        /// Gets or sets the neighbors.
        /// </summary>
        /// <value>
        /// The neighbors.
        /// </value>
        public List<string> Neighbors { get; set; }

        /// <summary>
        /// Gets the short name.
        /// </summary>
        /// <value>
        /// The short name.
        /// </value>
        public override string ShortName => "Hello";

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        public override object Clone()
        {
            var clone = new OlsrHelloMessage()
            {
                Neighbors = new List<string>(this.Neighbors),
                MultiPointRelays = new List<string>(this.MultiPointRelays)
            };

            return this.CopyTo(clone);
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

            builder.AppendLine(base.ToString());

            if (this.Neighbors?.Count > 0)
            {
                builder.AppendFormat("| Neighbors: {0}\n", string.Join(",", this.Neighbors));
            }

            if (this.MultiPointRelays?.Count > 0)
            {
                builder.AppendFormat("| MPRs: {0}\n", string.Join(",", this.MultiPointRelays));
            }

            builder.AppendLine($"+[/{this.GetType().Name}]");

            return builder.ToString();
        }
    }
}