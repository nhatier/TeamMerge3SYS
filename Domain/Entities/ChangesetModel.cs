using System;

namespace Domain.Entities
{
    public class Changeset : IEquatable<Changeset>
    {
        public int ChangesetId { get; set; }

        public string Owner { get; set; }

        public DateTime CreationDate { get; set; }

        public string Comment { get; set; }

        public bool Equals(Changeset other)
        {
            return other != null && ChangesetId == other.ChangesetId;
        }

        public override int GetHashCode()
        {
            return ChangesetId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is Changeset c) return Equals(c);
            return false;
        }
    }
}