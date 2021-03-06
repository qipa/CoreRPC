﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CoreRPC.Transport.Tcp
{
    public class TcpClientTransport : IClientTransport
    {
        private readonly int _minimumPoolSize;
        private readonly bool _enableMultiplexing;
        private readonly TcpConnectionPool _pool;

        public TcpClientTransport(IPAddress host, int port, int minimumPoolSize = 5, bool enableMultiplexing = true)
        {
            if (host == null) throw new ArgumentNullException("host");
            _pool = new TcpConnectionPool(new[] {new TcpRemote(host, port)});
            _minimumPoolSize = minimumPoolSize;
            _enableMultiplexing = enableMultiplexing;
        }

        async Task<byte[]> SendViaNewConnection(byte[] message)
        {
            var conn = await _pool.GetNewConnection (_enableMultiplexing);
            byte[] rv;
            try
            {
                rv = await conn.SendMessageAsync (message);
            }
            catch
            {
                conn.Dispose ();
                throw;
            }
            if (!_enableMultiplexing)
                _pool.AddConnection (conn);
            return rv;
        }

        async Task<byte[]> SendViaPoolConnection(byte[] message)
        {
            var conn = _pool.GetConnection(!_enableMultiplexing);
            byte[] rv;
            try
            {
                rv = await conn.SendMessageAsync(message);
            }
            catch
            {
                if (!_enableMultiplexing)
                    conn.Dispose();
                throw;
            }
            if(!_enableMultiplexing)
                _pool.AddConnection(conn);
            return rv;
        }

        public async Task<byte[]> SendMessageAsync(byte[] message)
        {
            if (_pool.Count < _minimumPoolSize)
                return await SendViaNewConnection(message);

            try
            {
                return await SendViaPoolConnection(message);
            }
// ReSharper disable EmptyGeneralCatchClause
            catch
// ReSharper restore EmptyGeneralCatchClause
            {
                //Ignore pooled connection error
            }
            return await SendViaNewConnection(message);
        }
    }
}
