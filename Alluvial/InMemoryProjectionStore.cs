﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
#pragma warning disable 1998

namespace Alluvial
{
    internal class InMemoryProjectionStore<TProjection> :
        IProjectionStore<string, TProjection>,
        IEnumerable<TProjection>
    {
        private readonly ConcurrentDictionary<string, TProjection> store = new ConcurrentDictionary<string, TProjection>();
        private readonly Func<string, TProjection> createProjection;

        public InMemoryProjectionStore(Func<string, TProjection> createProjection = null)
        {
            this.createProjection = createProjection ?? (_ => Activator.CreateInstance<TProjection>());
        }

        public async Task Put(string streamId, TProjection projection)
        {
            if (streamId == null)
            {
                throw new ArgumentNullException("streamId");
            }
            if (projection == null)
            {
                throw new ArgumentNullException("projection");
            }
            store[streamId] = projection;
        }

        public async Task<TProjection> Get(string streamId)
        {
            TProjection projection;
            if (store.TryGetValue(streamId, out projection))
            {
                return projection;
            }
            projection = createProjection(streamId);
            return projection;
        }

        public IEnumerator<TProjection> GetEnumerator()
        {
            return store.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}