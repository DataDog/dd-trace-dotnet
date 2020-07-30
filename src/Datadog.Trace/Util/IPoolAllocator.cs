namespace Datadog.Trace.Util
{
    /// <summary>
    /// Pool allocator interface
    /// </summary>
    /// <typeparam name="TObject">Type of object to allocate</typeparam>
    internal interface IPoolAllocator<TObject>
    {
        /// <summary>
        /// Allocates a new instance of the object
        /// </summary>
        /// <returns>Returns a new instance of the object</returns>
        TObject New();

        /// <summary>
        /// Clears the instance of an object before returning to the pool
        /// </summary>
        /// <param name="value">Instance of the object</param>
        void Clear(TObject value);
    }

    /// <summary>
    /// Default allocator
    /// </summary>
    /// <typeparam name="TObject">Type of object to allocate</typeparam>
    internal readonly struct DefaultAllocator<TObject> : IPoolAllocator<TObject>
        where TObject : new()
    {
        public void Clear(TObject value)
        {
            // Do nothing
        }

        public TObject New()
        {
            return new TObject();
        }
    }
}
