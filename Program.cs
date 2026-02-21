using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Text;
using System.Diagnostics;

static class Program
{
    static SemanticModel model;
    static StringBuilder builder;
    readonly static Dictionary<string, string> ctypes = new(){{"Single", "float"}, {"Int32", "int"}};

    static void EmitObjectCreation(ExpressionSyntax expressionSyntax, string varname)
    {
        if(expressionSyntax is InitializerExpressionSyntax initializerExpressionSyntax)
        {
            foreach(var e in initializerExpressionSyntax.Expressions)
            {
                if(e is AssignmentExpressionSyntax assignmentExpressionSyntax)
                {
                    var value = Emit(assignmentExpressionSyntax.Right);
                    if(assignmentExpressionSyntax.Left is SimpleNameSyntax simpleNameSyntax)
                    {
                        builder.AppendLine($"{varname}.{simpleNameSyntax} = {value};");
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }
        else
        {
            throw new NotImplementedException(expressionSyntax.GetType().Name);
        }
    }

/*
    static bool HasCAttribute(MemberDeclarationSyntax memberDeclarationSyntax)
    {
        foreach(var al in memberDeclarationSyntax.AttributeLists)
        {
            foreach(var a in al.Attributes)
            {
                if(a.Name.ToString() == "C")
                {
                    return true;
                }
            }
        }
        return false;
    }*/

    static string Emit(ExpressionSyntax expressionSyntax)
    {
        if(expressionSyntax is InvocationExpressionSyntax invocationExpressionSyntax)
        {
            var methodSymbol = model.GetSymbolInfo(invocationExpressionSyntax).Symbol as IMethodSymbol;
            var syntaxReference = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            var declaration = (MethodDeclarationSyntax)syntaxReference.GetSyntax();
            if (declaration.Modifiers.Any(SyntaxKind.ExternKeyword))
            {
                if(declaration.Identifier.ValueText == "Print")
                {
                    var value = Emit(invocationExpressionSyntax.ArgumentList.Arguments.First().Expression);
                    var args = "\"%s\\n\", " + value;
                    return $"printf({args})";
                }
                else
                {
                    var args = invocationExpressionSyntax
                        .ArgumentList.Arguments.Select(a=>Emit(a.Expression))
                        .ToArray();
                    return declaration.Identifier.ValueText+"("+string.Join(", ", args)+")";
                } 
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        else if(expressionSyntax is ElementAccessExpressionSyntax elementAccessExpressionSyntax)
        {
            var expr = Emit(elementAccessExpressionSyntax.Expression);
            var arg = Emit(elementAccessExpressionSyntax.ArgumentList.Arguments[0].Expression);
            return $"{expr}[{arg}]";
        }
        else if(expressionSyntax is CollectionExpressionSyntax collectionExpressionSyntax)
        {
            var typeInfo = model.GetTypeInfo(collectionExpressionSyntax);
            if (typeInfo.ConvertedType is IArrayTypeSymbol arrayType)
            {
                var typename = arrayType.ElementType.Name;
                if(ctypes.TryGetValue(typename, out var ctypename))
                {
                    typename = ctypename;
                }
                List<string> elements = [];
                foreach(var e in collectionExpressionSyntax.Elements)
                {
                    if(e is ExpressionElementSyntax expressionElementSyntax)
                    {
                        elements.Add(Emit(expressionElementSyntax.Expression));
                    }
                    else if(e is SpreadElementSyntax spreadElementSyntax)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                var count = collectionExpressionSyntax.Elements.Count;
                return $"({typename}[{count}]){{{string.Join(", ", elements)}}}";
            }
            else
            {
                throw new NotImplementedException();
            }
            
        }
        else if(expressionSyntax is BinaryExpressionSyntax binaryExpressionSyntax)
        {
            var left = Emit(binaryExpressionSyntax.Left);
            var op = binaryExpressionSyntax.OperatorToken.ToString();
            var right = Emit(binaryExpressionSyntax.Right);
            return $"({left} {op} {right})";
        }
        else if(expressionSyntax is IdentifierNameSyntax identifierNameSyntax)
        {
            return identifierNameSyntax.Identifier.ValueText;
        }
        else if(expressionSyntax is ImplicitObjectCreationExpressionSyntax implicitObjectCreationExpressionSyntax)
        {
            throw new NotImplementedException();
        }
        else if(expressionSyntax is ObjectCreationExpressionSyntax objectCreationExpressionSyntax)
        {
            var typename = objectCreationExpressionSyntax.Type.ToString();
            var args = objectCreationExpressionSyntax
                .ArgumentList
                .Arguments
                .Select(a=>Emit(a.Expression));
            return $"({typename}){{{string.Join(", ", args)}}}";
        }
        else if(expressionSyntax is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
        {
            var symbol = model.GetSymbolInfo(memberAccessExpressionSyntax).Symbol;
            if (symbol is IPropertySymbol propertySymbol)
            {
                var syntax = propertySymbol.DeclaringSyntaxReferences.FirstOrDefault().GetSyntax();
                var declaration = (PropertyDeclarationSyntax)syntax;
                if (declaration.Modifiers.Any(SyntaxKind.ExternKeyword))
                {
                    return declaration.Identifier.ValueText;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if(symbol is IFieldSymbol fieldSymbol)
            {
                var name = memberAccessExpressionSyntax.Name.ToString();
                var syntax= fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault().GetSyntax();
                if(syntax is EnumMemberDeclarationSyntax enumMemberDeclarationSyntax)
                {
                    return name;
                }
                else
                {
                    var expr = Emit(memberAccessExpressionSyntax.Expression);
                    return $"{expr}.{name}";
                }
            }
            else
            {
                throw new NotImplementedException(symbol.GetType().Name);
            }
        }
        else if(expressionSyntax is LiteralExpressionSyntax literalExpressionSyntax)
        {
            if(literalExpressionSyntax.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return '"'+literalExpressionSyntax.Token.ValueText+'"';
            }
            else if(literalExpressionSyntax.IsKind(SyntaxKind.TrueLiteralExpression))
            {
                return "1";
            }
            else if(literalExpressionSyntax.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                return "0";
            }
            else if (literalExpressionSyntax.IsKind(SyntaxKind.NumericLiteralExpression))
            {
                return literalExpressionSyntax.Token.ValueText;
            }
            else
            {
                throw new NotImplementedException(literalExpressionSyntax.Kind().ToString());
            }
        }
        else if(expressionSyntax is PrefixUnaryExpressionSyntax prefixUnaryExpressionSyntax)
        {
            if(prefixUnaryExpressionSyntax.OperatorToken.IsKind(SyntaxKind.ExclamationToken))
            {
                return $"!{Emit(prefixUnaryExpressionSyntax.Operand)}";
            }
            else if (prefixUnaryExpressionSyntax.OperatorToken.IsKind(SyntaxKind.MinusToken))
            {
                return $"-{Emit(prefixUnaryExpressionSyntax.Operand)}";
            }
            else
            {
                throw new NotImplementedException(prefixUnaryExpressionSyntax.OperatorToken.Kind().ToString());
            }
        }
        else if(expressionSyntax is InitializerExpressionSyntax initializerExpressionSyntax)
        {
            var exprs = initializerExpressionSyntax.Expressions.Select(Emit);
            return $"{{{string.Join(", ", exprs)}}}";
        }
        else if(expressionSyntax is AssignmentExpressionSyntax assignmentExpressionSyntax)
        {
            var right = Emit(assignmentExpressionSyntax.Right);
            var left = Emit(assignmentExpressionSyntax.Left);
            var op = assignmentExpressionSyntax.OperatorToken.ValueText;
            return $"{left} {op} {right}";
        }
        else
        {
            throw new NotImplementedException(expressionSyntax.GetType().Name);
        }
    }

    static void Emit(StatementSyntax statementSyntax)
    {
        if(statementSyntax is ExpressionStatementSyntax expressionStatementSyntax)
        {
            builder.AppendLine(Emit(expressionStatementSyntax.Expression)+";");
        }
        else if(statementSyntax is WhileStatementSyntax whileStatementSyntax)
        {
            var condition = Emit(whileStatementSyntax.Condition);
            builder.AppendLine($"while({condition})");
            Emit(whileStatementSyntax.Statement);
        }
        else if(statementSyntax is BlockSyntax blockSyntax)
        {
            Emit(blockSyntax);
        }
        else if(statementSyntax is LocalDeclarationStatementSyntax localDeclarationStatementSyntax)
        {
            var decl = localDeclarationStatementSyntax.Declaration;
            string typename;
            bool isArray = false;
            if (decl.Type.IsVar)
            {
                var symbolInfo = model.GetSymbolInfo(localDeclarationStatementSyntax.Declaration.Type);
                if(symbolInfo.Symbol is IArrayTypeSymbol arrayTypeSymbol)
                {
                    typename = arrayTypeSymbol.ElementType.Name;
                    isArray = true;
                }
                else
                {
                    typename = symbolInfo.Symbol.Name.ToString();
                }
            }
            else
            {
                if(decl.Type is ArrayTypeSyntax arrayTypeSyntax)
                {
                    typename = arrayTypeSyntax.ElementType.ToString();
                    isArray = true;
                }
                else
                {
                    typename = decl.Type.ToString();
                }
            }
            if(ctypes.TryGetValue(typename, out var ctypename))
            {
                typename = ctypename;
            }
            var fullCTypeName = isArray?typename+"*":typename;

            var variable = decl.Variables[0];
            var varname = variable.Identifier.ValueText;
           
            var expr = variable.Initializer.Value;
            if(expr is ImplicitObjectCreationExpressionSyntax implicitObjectCreationExpressionSyntax)
            {
                builder.AppendLine($"{fullCTypeName} {varname} = {{0}};");
                EmitObjectCreation(implicitObjectCreationExpressionSyntax.Initializer, varname);
            }
            else
            {
                builder.AppendLine($"{fullCTypeName} {varname} = {Emit(expr)};");
            }
        }
        else
        {
            throw new NotImplementedException(statementSyntax.GetType().Name);
        }
    }

    static void Emit(BlockSyntax blockSyntax)
    {
        builder.AppendLine("{");
        foreach(var s in blockSyntax.Statements)
        {
            Emit(s);
        }
        builder.AppendLine("}");
    }

    static void Main()
    {
        var source = File.ReadAllText("../FPS/Program.cs");
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("J.dll",
                   [tree],
                   [MetadataReference.CreateFromFile(typeof(float).Assembly.Location)],
                   new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        model = compilation.GetSemanticModel(tree);
        var main = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Main");
        
        builder = new();
        builder.AppendLine("#include <stdio.h>");
        builder.AppendLine("#include \"raylib.h\"");
        builder.AppendLine("#include \"raymath.h\"");
        builder.AppendLine("#define RLIGHTS_IMPLEMENTATION");
        builder.AppendLine("#include \"rlights.h\"");

        builder.AppendLine("int main()");
        Emit(main.Body);
        File.WriteAllText("c/main.c", builder.ToString());
        var process = Process.Start("gcc", "c/main.c -o c/main.out -l raylib -lm");
        process.WaitForExit();
        Process.Start("c/main.out");
    }
}