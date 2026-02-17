// --- In file: DelegateCacheKey.cs ---

// --- In file: DelegateCacheKey.cs ---
using System;

namespace Chat2Report.Providers
{
    

    public readonly struct DelegateCacheKey : IEquatable<DelegateCacheKey>
    {
        public string TypeName { get; }
        public string MethodName { get; }
       

        // Cache the hash code for performance.
        // *** HASH CODE NOW EXCLUDES PURPOSE ***
        private readonly int _hashCode;

        public DelegateCacheKey(string typeName, string methodName)
        {
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
            

            // *** Precompute hash code using only TypeName and MethodName ***
            _hashCode = HashCode.Combine(TypeName, MethodName);
        }

        public bool Equals(DelegateCacheKey other)
        {
            // *** EQUALS MUST STILL CHECK PURPOSE ***
            // Purpose is checked first as it's cheapest.
            // StringComparison.Ordinal is generally preferred for internal identifiers.
            return 
                   string.Equals(MethodName, other.MethodName, StringComparison.Ordinal) &&
                   string.Equals(TypeName, other.TypeName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is DelegateCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Return the precomputed hash code (which excludes Purpose)
            return _hashCode;
        }

        public override string ToString()
        {
            // Keep Purpose in ToString for clarity in logging
            return $"{TypeName}:{MethodName}";
        }

        public static bool operator ==(DelegateCacheKey left, DelegateCacheKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DelegateCacheKey left, DelegateCacheKey right)
        {
            return !(left == right);
        }
    }
}