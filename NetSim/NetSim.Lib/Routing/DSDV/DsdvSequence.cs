﻿using System;
using System.Linq;

using NetSim.Lib.Simulator;

namespace NetSim.Lib.Routing.DSDV
{
    /// <summary>
    /// 
    /// </summary>
    public class DsdvSequence : NetSimSequence, IEquatable<DsdvSequence>, IComparable<DsdvSequence>
    {

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(DsdvSequence other)
        {
            if (other == null) return false;

            return (this.SequenceId.Equals(other.SequenceId) && this.SequenceNr == other.SequenceNr);
        }

        /// <summary>
        /// Compares to.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        /// <exception cref="System.InvalidOperationException">Can't compare this sequences.</exception>
        public int CompareTo(DsdvSequence other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            if (!other.SequenceId.Equals(this.SequenceId))
            {
                throw new InvalidOperationException("Can't compare this sequences.");
            }

            if(this.SequenceNr > other.SequenceNr)
            {
                return 1;
            }

            if(this.SequenceNr < other.SequenceNr)
            {
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override object Clone()
        {
            return new DsdvSequence() { SequenceId = this.SequenceId, SequenceNr = this.SequenceNr };
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"{SequenceId}-{SequenceNr:000}";
        }
    }
}