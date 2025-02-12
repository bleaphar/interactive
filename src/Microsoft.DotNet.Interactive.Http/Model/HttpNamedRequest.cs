﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.DotNet.Interactive.Http.Parsing;
using System.Linq;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Interactive.Http;

internal class HttpNamedRequest
{
    internal HttpNamedRequest(HttpRequestNode httpRequestNode, HttpResponse response)
    {
        RequestNode = httpRequestNode;
        Name = RequestNode.TryGetCommentNamedRequestNode()?.ValueNode?.Text;
        Response = response;
    }

    public string? Name { get; }

    private readonly HttpRequestNode RequestNode;

    private readonly HttpResponse Response;

    public HttpBindingResult<object?> ResolvePath(string[] path, HttpExpressionNode node)
    {
        if (path.Length < 4)
        {
            return node.CreateBindingFailure(HttpDiagnostics.InvalidNamedRequestPath(node.Text));
        }

        if (path[SyntaxDepth.RequestName] != Name)
        {
            return node.CreateBindingFailure(HttpDiagnostics.InvalidNamedRequestPath(node.Text));
        }


        if (path[SyntaxDepth.RequestOrResponse] == "request")
        {
            if (path[SyntaxDepth.BodyOrHeaders] == "body")
            {
                if (RequestNode.BodyNode is null)
                {
                    return node.CreateBindingFailure(HttpDiagnostics.InvalidBodyInNamedRequest(path[SyntaxDepth.RequestName]));
                }
                else
                {
                    if (path[SyntaxDepth.RawBodyContent] == "*")
                    {
                        return node.CreateBindingSuccess(RequestNode.BodyNode.Text);
                    }
                    else if (path[SyntaxDepth.JsonRoot] == "$")
                    {
                        try
                        {
                            var requestJSON = JObject.Parse(RequestNode.BodyNode.Text);

                            if (requestJSON is not null)
                            {
                                var resolvedPath = ResolveJsonPath(requestJSON, path, 4);
                                if (resolvedPath != null)
                                {
                                    return node.CreateBindingSuccess(resolvedPath);
                                }

                            }
                            else
                            {
                                return node.CreateBindingFailure(HttpDiagnostics.InvalidContentInNamedRequest(string.Join(".", path)));
                            }
                        }
                        catch (JsonException)
                        {
                            return node.CreateBindingFailure(HttpDiagnostics.InvalidContentInNamedRequest(string.Join(".", path)));
                        }

                    }
                    else if (path[SyntaxDepth.XmlRoot].StartsWith("//"))
                    {
                        try
                        {
                            var xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(RequestNode.BodyNode.Text);

                            var xmlNodes = xmlDoc.SelectNodes(path[SyntaxDepth.XmlRoot].Substring(1));

                            if (xmlNodes is { Count: 1 })
                            {
                                return node.CreateBindingSuccess(xmlNodes.Item(0)?.Value);
                            }
                            else
                            {
                                return node.CreateBindingFailure(HttpDiagnostics.InvalidXmlNodeInNamedRequest(path[SyntaxDepth.XmlRoot]));
                            }

                        }
                        catch (XmlException)
                        {
                            return node.CreateBindingFailure(HttpDiagnostics.InvalidContentInNamedRequest(string.Join(".", path)));
                        }

                    }
                }
            }
            else if (path[SyntaxDepth.BodyOrHeaders] == "headers")
            {
                if (RequestNode.HeadersNode is null)
                {
                    return node.CreateBindingFailure(HttpDiagnostics.InvalidHeadersInNamedRequest(path[SyntaxDepth.RequestName]));
                }

                var headerNode = RequestNode.HeadersNode.HeaderNodes.FirstOrDefault(hn => hn.NameNode?.Text == path[SyntaxDepth.HeaderName]);

                if (headerNode is null || headerNode.ValueNode is null)
                {
                    return node.CreateBindingFailure(HttpDiagnostics.InvalidHeaderNameInNamedRequest(path[SyntaxDepth.HeaderName]));
                }

                return node.CreateBindingSuccess(headerNode.ValueNode.Text);
            }
        }
        else if (path[SyntaxDepth.RequestOrResponse] == "response")
        {
            if (path[SyntaxDepth.BodyOrHeaders] == "body")
            {
                if (Response.Content is null)
                {
                    return node.CreateBindingFailure(HttpDiagnostics.InvalidContentInNamedRequest(string.Join(".", path)));
                }
                else
                {
                    if (path[SyntaxDepth.RawBodyContent] == "*")
                    {
                        return node.CreateBindingSuccess(Response.Content.Raw);
                    }
                    else if (path[SyntaxDepth.JsonRoot] == "$")
                    {

                        if (Response.Content.ContentType == null || !(Response.Content.ContentType.StartsWith("application/json")))
                        {
                            return node.CreateBindingFailure(HttpDiagnostics.InvalidContentType(
                                                                 Response.Content.ContentType ?? "null", 
                                                                 "application/json"));
                        }

                        try
                        {
                            JToken dataJToken = JToken.Parse(Response.Content.Raw);

                            if (dataJToken is not null)
                            {
                                var resolvedPath = ResolveJsonPath(dataJToken, path, 4);
                                if (resolvedPath != null)
                                {
                                    return node.CreateBindingSuccess(resolvedPath);
                                }

                            }
                            else
                            {
                                return node.CreateBindingFailure(HttpDiagnostics.InvalidContentInNamedRequest(string.Join(".", path)));
                            }
                        }
                        catch (JsonException)
                        {
                            return node.CreateBindingFailure(HttpDiagnostics.InvalidContentInNamedRequest(string.Join(".", path)));
                        }


                    }
                    else if (path[SyntaxDepth.XmlRoot].StartsWith("//"))
                    {
                        if (Response.Content is null)
                        {
                            return node.CreateBindingFailure(HttpDiagnostics.InvalidContentInNamedRequest(string.Join(".", path)));
                        }

                        if (Response.Content.ContentType != "application/xml")
                        {
                            return node.CreateBindingFailure(HttpDiagnostics.InvalidContentType(
                                                                 Response.Content.ContentType ?? "null",
                                                                 "application/xml"));
                        }

                        try
                        {
                            var xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(Response.Content.Raw);

                            //Remove the leading slash
                            var xmlNodes = xmlDoc.SelectNodes(path[SyntaxDepth.XmlRoot].Substring(1));

                            if (xmlNodes is { Count: 1 })
                            {
                                return node.CreateBindingSuccess(xmlNodes.Item(0)?.Value);
                            }
                            else
                            {
                                return node.CreateBindingFailure(HttpDiagnostics.InvalidXmlNodeInNamedRequest(path[SyntaxDepth.XmlRoot]));
                            }
                        }
                        catch (XmlException)
                        {
                            return node.CreateBindingFailure(HttpDiagnostics.InvalidContentInNamedRequest(string.Join(".", path)));
                        }
                    }
                }

            }
            else if (path[SyntaxDepth.BodyOrHeaders] == "headers")
            {

                if (Response.Headers.TryGetValue(path[SyntaxDepth.HeaderName], out var header) && header is not null)
                {
                    //If the path is response.headers.<headerName> and the header value is an array, return the first element
                    if (path.Length == 4)
                    {
                        return node.CreateBindingSuccess(header.First());
                    }
                    else if (path.Length == 5)
                    {
                        var headerValue = header.FirstOrDefault(h => h == path[4]);
                        if (headerValue != null)
                        {
                            return node.CreateBindingSuccess(headerValue);
                        }
                    }
                }
                else
                {
                    return node.CreateBindingFailure(HttpDiagnostics.InvalidHeaderNameInNamedRequest(path[SyntaxDepth.HeaderName]));
                }

            }
        }

        return node.CreateBindingFailure(HttpDiagnostics.InvalidNamedRequestPath(node.Text));
    }

    private static object? ResolveJsonPath(JToken responseJSON, string[] path, int currentIndex)
    {

        JToken? token = null;
        var pathItem = path[currentIndex];

        switch(responseJSON)
        {
            case JArray jsonArray:
                var node = jsonArray.FirstOrDefault(n => n?[path[currentIndex]] != null);
                token = node?[path[currentIndex]];
                break;
            case JObject jsonObject:
                token = jsonObject.SelectToken(path[currentIndex]);
                break;
            default:
                token = responseJSON;
                break;
        }

        if(currentIndex + 1 == path.Length)
        {
            return token?.ToString();
        }

        if (token is not null)
        {
            return ResolveJsonPath(token, path, currentIndex + 1);
        }
        else
        {
            return null;
        }

    }

    internal static class SyntaxDepth
    {
        //They used to refer to the depth of different elements within the HttpNamedRequest syntax
        public const int RequestName = 0;
        public const int RequestOrResponse = 1;
        public const int BodyOrHeaders = 2;
        public const int XmlRoot = 3;
        public const int JsonRoot = 3;
        public const int HeaderName = 3;
        public const int RawBodyContent = 3; //Asterisk
    }
}

