using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Flecs.NET.Utilities;
using static Flecs.NET.Bindings.flecs;

namespace Flecs.NET.Core
{
    /// <summary>
    ///     A wrapper around ecs_query_t.
    /// </summary>
    public unsafe partial struct Query : IIterable, IEquatable<Query>, IDisposable
    {
        private ecs_world_t* _world;
        private ecs_query_t* _handle;

        /// <summary>
        ///     A reference to the world.
        /// </summary>
        public ref ecs_world_t* World => ref _world;

        /// <summary>
        ///     A reference to the handle.
        /// </summary>
        public ref ecs_query_t* Handle => ref _handle;

        /// <summary>
        ///     Creates a query from a handle.
        /// </summary>
        /// <param name="query">The query pointer.</param>
        public Query(ecs_query_t* query)
        {
            _world = query->world;
            _handle = query;
        }

        /// <summary>
        ///     Creates a query from a world and handle.
        /// </summary>
        /// <param name="world">The world.</param>
        /// <param name="query">The query pointer.</param>
        public Query(ecs_world_t* world, ecs_query_t* query = null)
        {
            _world = world;
            _handle = query;
        }

        /// <summary>
        ///     Creates a query from an entity.
        /// </summary>
        /// <param name="world">The world.</param>
        /// <param name="entity">The query entity.</param>
        public Query(ecs_world_t* world, ulong entity) : this (new Entity(world, entity))
        {
        }

        /// <summary>
        ///     Creates a query from an entity.
        /// </summary>
        /// <param name="entity">The query entity.</param>
        public Query(Entity entity)
        {
            _world = entity.World;

            if (entity != 0)
            {
                EcsPoly* poly = entity.GetPtr<EcsPoly>(EcsQuery);

                if (poly != null)
                {
                    _handle = (ecs_query_t*)poly->poly;
                    return;
                }
            }

            ecs_query_desc_t desc = default;
            _handle = ecs_query_init(_world, &desc);
        }

        /// <summary>
        ///     Disposes query.
        /// </summary>
        public void Dispose()
        {
            Destruct();
        }

        /// <summary>
        ///     Destructs query and cleans up resources.
        /// </summary>
        public void Destruct()
        {
            if (Handle == null)
                return;

            ecs_query_fini(Handle);
            World = null;
            Handle = null;
        }

        /// <summary>
        ///     Returns the entity associated with the query.
        /// </summary>
        /// <returns></returns>
        public Entity Entity()
        {
            return new Entity(World, Handle->entity);
        }

        /// <summary>
        ///     Returns the query handle.
        /// </summary>
        /// <returns></returns>
        public ecs_query_t* CPtr()
        {
            return Handle;
        }

        /// <summary>
        ///     Returns whether the query data changed since the last iteration.
        /// </summary>
        /// <returns></returns>
        public bool Changed()
        {
            return Macros.Bool(ecs_query_changed(Handle));
        }

        /// <summary>
        ///     Get info for group.
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public ecs_query_group_info_t* GroupInfo(ulong groupId)
        {
            return ecs_query_get_group_info(Handle, groupId);
        }

        /// <summary>
        ///     Get context for group.
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public void* GroupCtx(ulong groupId)
        {
            ecs_query_group_info_t* groupInfo = GroupInfo(groupId);
            return groupInfo == null ? null : groupInfo->ctx;
        }

        /// <summary>
        ///     Iterates terms with the provided callback.
        /// </summary>
        /// <param name="callback"></param>
        public void EachTerm(Ecs.TermCallback callback)
        {
            for (int i = 0; i < Handle->term_count; i++)
            {
                Term term = new Term(World, Handle->terms[i]);
                callback(ref term);
            }
        }

        /// <summary>
        ///     Gets term at provided index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Term Term(int index)
        {
            Ecs.Assert(index < Handle->term_count, nameof(ECS_COLUMN_INDEX_OUT_OF_RANGE));
            return new Term(World, Handle->terms[index]);
        }

        /// <summary>
        ///     Gets term count.
        /// </summary>
        /// <returns></returns>
        public int TermCount()
        {
            return Handle->term_count;
        }

        /// <summary>
        ///     Gets field count.
        /// </summary>
        /// <returns></returns>
        public int FieldCount()
        {
            return Handle->field_count;
        }

        /// <summary>
        ///     Searches for a variable by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public int FindVar(string name)
        {
            using NativeString nativeName = (NativeString)name;
            return ecs_query_find_var(Handle, nativeName);
        }

        /// <summary>
        ///     Returns the string of the query.
        /// </summary>
        /// <returns></returns>
        public string Str()
        {
            return NativeString.GetStringAndFree(ecs_query_str(Handle));
        }

        /// <summary>
        ///     Returns a string representing the query plan.
        /// </summary>
        /// <returns></returns>
        public string Plan()
        {
            return NativeString.GetStringAndFree(ecs_query_plan(Handle));
        }

        /// <summary>
        ///     Iterates the query using the provided callback.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public void Iter(Ecs.IterCallback callback)
        {
            ecs_iter_t iter = GetIter();
            while (GetNext(&iter))
                Invoker.Iter(&iter, callback);
        }

        /// <summary>
        ///     Iterates the query using the provided callback.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public void Each(Ecs.EachEntityCallback callback)
        {
            ecs_iter_t iter = GetIter();
            while (GetNextInstanced(&iter))
                Invoker.Each(&iter, callback);
        }

        /// <summary>
        ///     Iterates the query using the provided callback.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public void Each(Ecs.EachIterCallback callback)
        {
            ecs_iter_t iter = GetIter();
            while (GetNextInstanced(&iter))
                Invoker.Each(&iter, callback);
        }

        /// <summary>
        ///     Iterates the query using the provided callback.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public void Run(Ecs.RunCallback callback)
        {
            ecs_iter_t iter = GetIter();
            Invoker.Run(&iter, callback);
        }

#if NET5_0_OR_GREATER
        /// <summary>
        ///     Iterates the query using the provided callback.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public void Iter(delegate*<Iter, void> callback)
        {
            ecs_iter_t iter = GetIter();
            while (GetNext(&iter))
                Invoker.Iter(&iter, callback);
        }

        /// <summary>
        ///     Iterates the query using the provided callback.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public void Each(delegate*<Entity, void> callback)
        {
            ecs_iter_t iter = GetIter();
            while (GetNextInstanced(&iter))
                Invoker.Each(&iter, callback);
        }

        /// <summary>
        ///     Iterates the query using the provided callback.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public void Each(delegate*<Iter, int, void> callback)
        {
            ecs_iter_t iter = GetIter();
            while (GetNextInstanced(&iter))
                Invoker.Each(&iter, callback);
        }

        /// <summary>
        ///     Iterates the query using the provided callback.
        /// </summary>
        /// <param name="callback">The callback.</param>
        public void Run(delegate*<Iter, void> callback)
        {
            ecs_iter_t iter = GetIter();
            Invoker.Run(&iter, callback);
        }
#endif

        /// <summary>
        ///     Converts a <see cref="Query"/> instance to an <see cref="ecs_query_t"/>*.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static ecs_query_t* To(Query query)
        {
            return query.Handle;
        }

        /// <summary>
        ///     Returns true if query handle is not a null pointer, otherwise return false.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static bool ToBoolean(Query query)
        {
            return query.Handle != null;
        }

        /// <summary>
        ///     Converts a <see cref="Query"/> instance to an <see cref="ecs_query_t"/>*.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static implicit operator ecs_query_t*(Query query)
        {
            return To(query);
        }

        /// <summary>
        ///     Returns true if query handle is not a null pointer, otherwise return false.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static implicit operator bool(Query query)
        {
            return ToBoolean(query);
        }

        /// <summary>
        ///     Checks if two <see cref="Query"/> instances are equal.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(Query other)
        {
            return Handle == other.Handle;
        }

        /// <summary>
        ///     Checks if two <see cref="Query"/> instances are equal.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object? obj)
        {
            return obj is Query other && Equals(other);
        }

        /// <summary>
        ///     Returns the hash code of the <see cref="Query"/>.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Handle->GetHashCode();
        }

        /// <summary>
        ///     Checks if two <see cref="Query"/> instances are equal.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(Query left, Query right)
        {
            return left.Equals(right);
        }

        /// <summary>
        ///     Checks if two <see cref="Query"/> instances are not equal.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(Query left, Query right)
        {
            return !(left == right);
        }
    }

    // IIterable Interface
    public unsafe partial struct Query
    {
        /// <inheritdoc cref="IIterable.GetIter"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ecs_iter_t GetIter(ecs_world_t* world = null)
        {
            Ecs.Assert(Handle != null, "Cannot iterate invalid query.");

            if (world == null)
                world = Handle->world;

            return ecs_query_iter(world, Handle);
        }

        /// <inheritdoc cref="IIterable.GetNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetNext(ecs_iter_t* it)
        {
            return Macros.Bool(ecs_query_next(it));
        }

        /// <inheritdoc cref="IIterable.GetNextInstanced"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetNextInstanced(ecs_iter_t* it)
        {
            return Macros.Bool(flecs_query_next_instanced(it));
        }

        /// <inheritdoc cref="IIterable.Iter(Flecs.NET.Core.World)"/>
        public IterIterable Iter(World world = default)
        {
            return new IterIterable(GetIter(world), IterableType.Query);
        }

        /// <inheritdoc cref="IIterable.Iter(Flecs.NET.Core.Iter)"/>
        public IterIterable Iter(Iter it)
        {
            return Iter(it.World());
        }

        /// <inheritdoc cref="IIterable.Iter(Flecs.NET.Core.Entity)"/>
        public IterIterable Iter(Entity entity)
        {
            return Iter(entity.CsWorld());
        }

        /// <inheritdoc cref="IIterable.Page(int, int)"/>
        public PageIterable Page(int offset, int limit)
        {
            return new PageIterable(GetIter(), offset, limit);
        }

        /// <inheritdoc cref="IIterable.Worker(int, int)"/>
        public WorkerIterable Worker(int index, int count)
        {
            return new WorkerIterable(GetIter(), index, count);
        }

        /// <inheritdoc cref="IIterable.Count()"/>
        public int Count()
        {
            return Iter().Count();
        }

        /// <inheritdoc cref="IIterable.IsTrue()"/>
        public bool IsTrue()
        {
            return Iter().IsTrue();
        }

        /// <inheritdoc cref="IIterable.First()"/>
        public Entity First()
        {
            return Iter().First();
        }

        /// <inheritdoc cref="IIterable.SetVar(int, ulong)"/>
        public IterIterable SetVar(int varId, ulong value)
        {
            return Iter().SetVar(varId, value);
        }

        /// <inheritdoc cref="IIterable.SetVar(string, ulong)"/>
        public IterIterable SetVar(string name, ulong value)
        {
            return Iter().SetVar(name, value);
        }

        /// <inheritdoc cref="IIterable.SetVar(string, ecs_table_t*)"/>
        public IterIterable SetVar(string name, ecs_table_t* value)
        {
            return Iter().SetVar(name, value);
        }

        /// <inheritdoc cref="IIterable.SetVar(string, ecs_table_range_t)"/>
        public IterIterable SetVar(string name, ecs_table_range_t value)
        {
            return Iter().SetVar(name, value);
        }

        /// <inheritdoc cref="IIterable.SetVar(string, Table)"/>
        public IterIterable SetVar(string name, Table value)
        {
            return Iter().SetVar(name, value);
        }

        /// <inheritdoc cref="IIterable.SetGroup(ulong)"/>
        public IterIterable SetGroup(ulong groupId)
        {
            return Iter().SetGroup(groupId);
        }

        /// <inheritdoc cref="IIterable.SetGroup{T}()"/>
        public IterIterable SetGroup<T>()
        {
            return Iter().SetGroup<T>();
        }
    }
}
