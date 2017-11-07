﻿using System;
using System.Collections.Generic;
using System.Threading;
using Vim.EditorHost;
using Vim.UnitTest.Utilities;

namespace Vim.UnitTest
{
    public sealed class TestableSynchronizationContextEventArgs : EventArgs
    {
        public TestableSynchronizationContext TestableSynchronizationContext { get; }

        public TestableSynchronizationContextEventArgs(TestableSynchronizationContext context)
        {
            TestableSynchronizationContext = context;
        }
    }

    public sealed class TestableSynchronizationContext : SynchronizationContext, IDisposable
    {
        #region Static Data 
        private static readonly object s_guard = new object();
        private static event EventHandler<TestableSynchronizationContextEventArgs> s_createdEvent;

        public static event EventHandler<TestableSynchronizationContextEventArgs> Created
        {
            add
            {
                lock (s_guard)
                {
                    s_createdEvent += value;
                }
            }
            remove
            {
                lock (s_guard)
                {
                    s_createdEvent -= value;
                }
            }
        }

        #endregion

        private SynchronizationContext _oldSynchronizationContext;
        private bool _isSet;
        private readonly Queue<Action> _queue = new Queue<Action>();

        public bool IsEmpty => 0 == _queue.Count;
        public bool IsDisposed { get; private set; }
        public int PostedCallbackCount => _queue.Count;
        public event EventHandler PostedCallback;

        public TestableSynchronizationContext(bool install = true)
        {
            if (install)
            {
                Install();
            }

            lock (s_guard)
            {
                s_createdEvent?.Invoke(this, new TestableSynchronizationContextEventArgs(this));
            }
        }

        public void Dispose()
        {
            if (_isSet)
            {
                Uninstall();
            }

            IsDisposed = true;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (d == null)
            {
                throw new ArgumentException(nameof(d));
            }

            CheckDisposed();
            _queue.Enqueue(() => d(state));
            PostedCallback?.Invoke(this, EventArgs.Empty);
        }

        public void RunOne()
        {
            CheckDisposed();

            if (_queue.Count == 0)
            {
                throw new InvalidOperationException();
            }

            var action = _queue.Dequeue();
            action();
        }

        public void RunAll()
        {
            CheckDisposed();

            while (_queue.Count > 0)
            {
                RunOne();
            }
        }

        public void Install()
        {
            if (_isSet)
            {
                throw new InvalidOperationException();
            }

            CheckDisposed();

            _oldSynchronizationContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(this);
            _isSet = true;
        }

        public void Uninstall()
        {
            if (!_isSet)
            {
                throw new InvalidOperationException();
            }

            if (PostedCallbackCount > 0)
            {
                throw new InvalidOperationException();
            }

            CheckDisposed();
            SynchronizationContext.SetSynchronizationContext(_oldSynchronizationContext);
            _oldSynchronizationContext = null;
            _isSet = false;
        }

        private void CheckDisposed()
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException("Object is disposed");
            }
        }
    }
}
