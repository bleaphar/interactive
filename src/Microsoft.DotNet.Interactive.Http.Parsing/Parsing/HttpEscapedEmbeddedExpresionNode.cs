using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Interactive.Http.Parsing
{
    internal class HttpEscapedEmbeddedExpressionNode : HttpEmbeddedExpressionNode
    {
        internal HttpEscapedEmbeddedExpressionNode(
            SourceText sourceText,
            HttpSyntaxTree syntaxTree) : base(sourceText, syntaxTree)
        {
        }

        public new HttpExpressionNode? ExpressionNode { get; private set; }

        public string ResolveExpression()
        {
            if (ExpressionNode is null)
            {
                throw new InvalidOperationException($"{nameof(ExpressionNode)} was not added.");
            }

            return "{{ " + ExpressionNode.Text + " }}";
        }

        public void Add(HttpExpressionNode expressionNode)
        {
            if (ExpressionNode is not null)
            {
                throw new InvalidOperationException($"{nameof(ExpressionNode)} was already added.");
            }

            ExpressionNode = expressionNode;
            AddInternal(ExpressionNode);
        }

    }
}
