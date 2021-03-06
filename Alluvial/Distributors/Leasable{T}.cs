using System;

namespace Alluvial.Distributors
{
    /// <summary>
    /// Represents some resource for which a lease can be acquired for exclusive access.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
    public class Leasable<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Leasable{T}"/> class.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <param name="name">The name.</param>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public Leasable(
            T resource,
            string name)
        {
            if (resource == null)
            {
                throw new ArgumentNullException("resource");
            }
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }
            Resource = resource;
            Name = name;
        }

        /// <summary>
        /// Gets the name of the resource.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets or sets the time at which the lease was last granted.
        /// </summary>
        public DateTimeOffset LeaseLastGranted { get; set; }

        /// <summary>
        /// Gets or sets the time at which the lease was last released.
        /// </summary>
        public DateTimeOffset LeaseLastReleased { get; set; }

        /// <summary>
        /// Gets the resource.
        /// </summary>
        public T Resource { get; private set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("leasable resource:{0} (granted @ {1}, released @ {2})",
                                 Name,
                                 LeaseLastGranted,
                                 LeaseLastReleased);
        }
    }
}