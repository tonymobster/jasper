﻿using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Baseline;
using Jasper.Conneg;
using Microsoft.AspNetCore.Http;

namespace Jasper.Http.Model
{
    public abstract class RouteHandler
    {
        private const string ResourceNotFound = "Resource not found";

        /// <summary>
        /// Handles the actual HTTP request
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="segments"></param>
        /// <returns></returns>
        public abstract Task Handle(HttpContext httpContext, string[] segments);



        public Task UseWriter(object model, HttpResponse response)
        {
            var writer = Writer;

            return UseWriter(model, response, writer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task UseWriter(object model, HttpResponse response, IMessageSerializer writer)
        {
            if (model == null)
            {
                response.StatusCode = 404;
                response.ContentType = "text/plain";
                response.ContentLength = ResourceNotFound.Length;

                return response.WriteAsync(ResourceNotFound);
            }
            else
            {
                response.ContentType = writer.ContentType;
                return writer.WriteToStream(model, response);
            }
        }


        public RouteChain Chain { get; set; }

        public IMessageDeserializer Reader { get; set; }
        public IMessageSerializer Writer { get; set; }
        public ModelReader ConnegReader { get; set; }
        public ModelWriter ConnegWriter { get; set; }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteText(string text, HttpResponse response)
        {
            response.Headers["content-type"] = "text/plain";
            response.Headers["content-length"] = text.Length.ToString();
            var bytes = Encoding.UTF8.GetBytes(text);
            return response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        public IMessageDeserializer SelectReader(HttpRequest request)
        {
            return ConnegReader[request.ContentType];
        }

        public IMessageSerializer SelectWriter(HttpRequest request)
        {
            return ConnegWriter.ChooseWriter(request.Headers["accept"]);
        }

        public string ToRelativePath(string[] segments, int starting)
        {
            return segments.Skip(starting).Join("/");
        }

        public string[] ToPathSegments(string[] segments, int starting)
        {
            return segments.Skip(starting).ToArray();
        }
    }
}
