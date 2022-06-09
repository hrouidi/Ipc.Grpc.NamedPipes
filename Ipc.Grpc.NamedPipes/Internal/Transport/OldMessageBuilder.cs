using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Ipc.Grpc.NamedPipes.Protocol;

namespace Ipc.Grpc.NamedPipes.Internal
{
    internal static class OldMessageBuilder
    {
        #region Client messages
        //TODO : cache bytes of these messages
        public static ClientMessage CancelRequest => new() { RequestControl = RequestControl.Cancel };
        public static ClientMessage StreamEnd => new() { RequestControl = RequestControl.StreamEnd };

        public static (ClientMessage message, byte[] payload) BuildRequest<TRequest, TResponse>(Method<TRequest, TResponse> method, TRequest request, DateTime? deadline, Metadata headers)
        {
            ClientMessage message = new()
            {
                Request = new Request
                {
                    MethodFullName = method.FullName,
                    MethodType = (Request.Types.MethodType)method.Type,
                    Deadline = deadline != null ? Timestamp.FromDateTime(deadline.Value) : null,
                    Headers = ToHeaders(headers),
                }
            };

            byte[] bytes = null;
            if (request != null)
            {
                bytes = SerializationHelpers.Serialize(method.RequestMarshaller, request);
                message.Request.PayloadSize = bytes.Length;

            }
            return (message, bytes);
        }

        public static ClientMessage BuildRequest2<TRequest, TResponse>(Method<TRequest, TResponse> method, TRequest request, DateTime? deadline, Metadata headers)
        {
            ClientMessage message = new()
            {
                Request = new Request
                {
                    MethodFullName = method.FullName,
                    MethodType = (Request.Types.MethodType)method.Type,
                    Deadline = deadline != null ? Timestamp.FromDateTime(deadline.Value) : null,
                    Headers = ToHeaders(headers),
                }
            };


            return message;
        }

        public static (ClientMessage message, byte[] payload) BuildStreamPayload<TRequest>(Marshaller<TRequest> marshaller, TRequest message)
        {
            byte[] payload = SerializationHelpers.Serialize(marshaller, message);
            ClientMessage ret = new()
            {
                StreamPayloadInfo = new StreamPayloadInfo
                {
                    PayloadSize = payload.Length,
                }
            };
            return (ret, payload);
        }

        #endregion

        #region Server messages

        public static ServerMessage BuildResponse(int size, Metadata trailers, StatusCode statusCode, string statusDetail)
        {
            Trailers transportTrailers = ToTrailers(trailers);
            transportTrailers.StatusCode = (int)statusCode;
            transportTrailers.StatusDetail = statusDetail;
            ServerMessage message = new()
            {
                Response = new Response
                {
                    PayloadSize = size,
                    Trailers = transportTrailers
                }
            };
            return message;
        }

        public static ServerMessage BuildUnaryResponse(Metadata trailers, StatusCode statusCode, string statusDetail)
        {
            Trailers transportTrailers = ToTrailers(trailers);
            transportTrailers.StatusCode = (int)statusCode;
            transportTrailers.StatusDetail = statusDetail;
            ServerMessage message = new()
            {
                Response = new Response
                {
                    Trailers = transportTrailers
                }
            };
            return message;
        }

        public static ServerMessage BuildResponseHeadersMessage(Metadata responseHeaders)
        {
            Headers headers = ToHeaders(responseHeaders);
            return new ServerMessage
            {
                ResponseHeaders = headers
            };
        }

        public static (ServerMessage message, byte[] payload) BuildResponseStreamPayload<TResponse>(Marshaller<TResponse> marshaller, TResponse message)
        {
            byte[] payload = SerializationHelpers.Serialize(marshaller, message);
            ServerMessage ret = new()
            {
                StreamPayloadInfo = new StreamPayloadInfo
                {
                    PayloadSize = payload.Length,
                }
            };
            return (ret, payload);
        }

        #endregion

        private static Headers ToHeaders(Metadata metadata)
        {
            var headers = new Headers();
            AddMetadata(metadata, headers.Metadata);
            return headers;

        }

        private static Trailers ToTrailers(Metadata metadata)
        {
            var ret = new Trailers();
            AddMetadata(metadata, ret.Metadata);
            return ret;

        }

        private static void AddMetadata(Metadata metadata, ICollection<MetadataEntry> accumulator)
        {
            foreach (Metadata.Entry entry in metadata ?? new Metadata())
            {
                var transportEntry = new MetadataEntry { Name = entry.Key };
                if (entry.IsBinary)
                    transportEntry.ValueBytes = ByteString.CopyFrom(entry.ValueBytes);
                else
                    transportEntry.ValueString = entry.Value;

                accumulator.Add(transportEntry);
            }

        }

        public static Metadata ToMetadata(RepeatedField<MetadataEntry> entries)
        {
            var metadata = new Metadata();
            foreach (MetadataEntry entry in entries)
            {
                switch (entry.ValueCase)
                {
                    case MetadataEntry.ValueOneofCase.ValueString:
                        metadata.Add(new Metadata.Entry(entry.Name, entry.ValueString));
                        break;
                    case MetadataEntry.ValueOneofCase.ValueBytes:
                        metadata.Add(new Metadata.Entry(entry.Name, entry.ValueBytes.ToByteArray()));
                        break;
                }
            }

            return metadata;
        }
    }
}