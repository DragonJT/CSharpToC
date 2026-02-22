using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Text;
using System.Diagnostics;


class TypeData(string name, bool isPrimitive, bool hasCAttribute)
{
    public string name = name;
    public bool isPrimitive = isPrimitive;
    public bool hasCAttribute = hasCAttribute;
}

static class Program
{
    static SemanticModel model;
    static StringBuilder builder;
    readonly static Dictionary<string, string> primitives = new(){{"Single", "float"}, {"Int32", "int"}};
    static readonly Dictionary<SyntaxNode, string> nodeNames = [];
    static readonly Dictionary<MemberDeclarationSyntax, string> defaultConstructors = [];
    static ClassDeclarationSyntax currentClass;
    static int id = 0;

    static int GetUniqueID()
    {
        var result = id;
        id++;
        return result;
    }

    static string InternalGetName(SyntaxNode node)
    {
        if(node is MethodDeclarationSyntax method)
        {
            var classDecl = method.Parent as ClassDeclarationSyntax;
            return classDecl.Identifier.ValueText + "_" + method.Identifier.ValueText+"_"+GetUniqueID();
        }
        else if(node is ConstructorDeclarationSyntax constructor)
        {
            var classDecl = constructor.Parent as ClassDeclarationSyntax;
            return classDecl.Identifier.ValueText+"_constructor_"+GetUniqueID();
        }
        else if(node is ClassDeclarationSyntax classDeclarationSyntax)
        {
            return classDeclarationSyntax.Identifier.ValueText;
        }
        else if(node is StructDeclarationSyntax structDeclarationSyntax)
        {
            return structDeclarationSyntax.Identifier.ValueText;
        }
        else
        {
            throw new NotImplementedException(node.GetType().Name);
        }
    }

    static string GetName(SyntaxNode node)
    {
        if(nodeNames.TryGetValue(node, out string name))
        {
            return name;
        }
        else
        {
            var newName = InternalGetName(node);
            nodeNames.Add(node, newName);
            return newName;
        }
    }

    static string GetDefaultConstructorName(ClassDeclarationSyntax classDeclarationSyntax)
    {
        if(defaultConstructors.TryGetValue(classDeclarationSyntax, out string name))
        {
            return name;
        }
        var newName = GetName(classDeclarationSyntax) + "_defaultConstructor_"+GetUniqueID();
        defaultConstructors.Add(classDeclarationSyntax, newName);
        return newName;
    }

    static TypeData GetTypeData(ITypeSymbol typeSymbol)
    {
        var refs = typeSymbol.DeclaringSyntaxReferences;
        if(refs.Length > 0)
        {
            var syntax = refs[0].GetSyntax();
            bool hasCAttribute = HasCAttribute((MemberDeclarationSyntax)syntax);
            var name = GetName(syntax);
            return new TypeData(name, false, hasCAttribute);
        }
        else
        {
            return new TypeData(primitives[typeSymbol.Name], true, false);
        }
    }

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
    }

    static string[] EmitArgs(ArgumentListSyntax args)
    {
        return args.Arguments.Select(a=>Emit(a.Expression)).ToArray();
    }

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
                    var args = EmitArgs(invocationExpressionSyntax.ArgumentList);
                    return declaration.Identifier.ValueText+"("+string.Join(", ", args)+")";
                } 
            }
            else
            {
                var arg1 = Emit(invocationExpressionSyntax.Expression);
                var name = GetName(declaration); 
                string[] args = [arg1, ..EmitArgs(invocationExpressionSyntax.ArgumentList)];
                return name+"("+string.Join(", ", args)+")";
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
                var typename = GetTypeData(arrayType.ElementType).name;
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
            var identifierSymbol = model.GetSymbolInfo(identifierNameSyntax).Symbol;
            if(identifierSymbol is IFieldSymbol fieldSymbol)
            {
                var classDecl = (ClassDeclarationSyntax)fieldSymbol.ContainingType.DeclaringSyntaxReferences
                    .FirstOrDefault().GetSyntax();
                if(currentClass == classDecl)
                {
                    return "this->"+identifierNameSyntax.Identifier.ValueText;
                }
                else
                {
                    throw new Exception();
                }
            }
            else
            {
                return identifierNameSyntax.Identifier.ValueText;
            }
        }
        else if(expressionSyntax is ImplicitObjectCreationExpressionSyntax implicitObjectCreationExpressionSyntax)
        {
            throw new NotImplementedException();
        }
        else if(expressionSyntax is ObjectCreationExpressionSyntax objectCreationExpressionSyntax)
        {
            var ctorSymbol = (IMethodSymbol)model.GetSymbolInfo(objectCreationExpressionSyntax).Symbol;
            var ctor = ctorSymbol.DeclaringSyntaxReferences.FirstOrDefault();

            if(ctor == null)
            {
                var typeDecl = (ClassDeclarationSyntax)ctorSymbol
                    .ContainingType.DeclaringSyntaxReferences
                    .First()
                    .GetSyntax();
                var name = GetDefaultConstructorName(typeDecl);
                var args = EmitArgs(objectCreationExpressionSyntax.ArgumentList);
                return $"{name}({string.Join(", ", args)})";
            }
            else
            {
                var syntax = ctor.GetSyntax() as MemberDeclarationSyntax;
                if (!HasCAttribute(syntax))
                {
                    var name = GetName(syntax);
                    var args = EmitArgs(objectCreationExpressionSyntax.ArgumentList);
                    return $"{name}({string.Join(", ", args)})";
                }
                else
                {
                    if(syntax is MemberDeclarationSyntax memberDeclarationSyntax)
                    {
                        var name = GetName(memberDeclarationSyntax);
                        var args = EmitArgs(objectCreationExpressionSyntax.ArgumentList);
                        return $"({name}){{{string.Join(", ", args)}}}";
                    }
                    else
                    {
                        throw new NotImplementedException(syntax.GetType().Name);
                    }
                }
            }
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
                return Emit(memberAccessExpressionSyntax.Expression);
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
            bool isPtr = false;

            var symbolInfo = model.GetSymbolInfo(localDeclarationStatementSyntax.Declaration.Type);
            if(symbolInfo.Symbol is IArrayTypeSymbol arrayTypeSymbol)
            {
                typename = GetTypeData(arrayTypeSymbol.ElementType).name;
                isPtr = true;
            }
            else if(symbolInfo.Symbol is ITypeSymbol typeSymbol)
            {
                var typedata = GetTypeData(typeSymbol);
                if (!typedata.hasCAttribute)
                {
                    isPtr = true;
                }
                typename = typedata.name;
            }
            else
            {
                throw new NotImplementedException();
            }
            var fullCTypeName = isPtr?typename+"*":typename;

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

    static void EmitConstructors(ClassDeclarationSyntax[] classDeclarationSyntaxes)
    {
        foreach(var c in classDeclarationSyntaxes)
        {
            var name = GetName(c);
            if (!HasCAttribute(c) && name!="CAttribute")
            {
                if (c.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    
                }
                else
                {
                    var constructors = c.DescendantNodes().OfType<ConstructorDeclarationSyntax>().ToArray();
                    if(constructors.Length == 0)
                    {
                        var constructorName = GetDefaultConstructorName(c);
                        var funcString = $@"{name}* {constructorName}()
{{
    return ({name}*)malloc(sizeof({name}));
}}
                        ";
                        builder.AppendLine(funcString);
                    }
                    else
                    {
                        throw new NotImplementedException(c.ToString());
                    }
                }
                
            }
            
        }
    }

    static void EmitMethods(MethodDeclarationSyntax[] methodDeclarationSyntaxes)
    {
        foreach(var m in methodDeclarationSyntaxes)
        {
            if(m.Identifier.ValueText == "Main")
            {
                currentClass = null;
                builder.AppendLine("int main()");
                Emit(m.Body);
                builder.AppendLine();
            }
            else if (m.Modifiers.Any(SyntaxKind.ExternKeyword) || m.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
            }
            else{
                currentClass = (ClassDeclarationSyntax)m.Parent;
                var name = GetName(m);
                var parameters = m.ParameterList.Parameters
                    .Select(p=>p.Type.ToString()+" "+p.Identifier.ValueText)
                    .ToArray();
                string[] finalParameters = [currentClass.Identifier.ValueText+" *this", ..parameters];
                var returnType = m.ReturnType.ToString();
                builder.AppendLine($"{returnType} {name}({string.Join(", ", finalParameters)})");
                Emit(m.Body);
                builder.AppendLine();
            }
        }
    }

    static void EmitClasses(ClassDeclarationSyntax[] classDeclarationSyntaxes)
    {
        foreach(var c in classDeclarationSyntaxes)
        {
            var name = GetName(c);
            if (!HasCAttribute(c) && name!="CAttribute")
            {
                builder.AppendLine($"typedef struct {name} {{");
                foreach(var f in c.Members.OfType<FieldDeclarationSyntax>())
                {
                    builder.AppendLine(
                        f.Declaration.Type.ToString()+" "
                        +f.Declaration.Variables[0].Identifier.ValueText+";");
                }
                builder.AppendLine($"}}{name};");
                builder.AppendLine();
            }
        }   
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
        builder = new();
        builder.AppendLine("#include <stdio.h>");
        builder.AppendLine("#include \"raylib.h\"");
        builder.AppendLine("#include \"raymath.h\"");
        builder.AppendLine("#define RLIGHTS_IMPLEMENTATION");
        builder.AppendLine("#include \"rlights.h\"");
        builder.AppendLine("#include <stdlib.h>");

        EmitClasses([.. tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()]);
        EmitConstructors([.. tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()]);
        EmitMethods([.. tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()]);

        File.WriteAllText("c/main.c", builder.ToString());
        var process = Process.Start("gcc", "c/main.c -o c/main.out -l raylib -lm");
        process.WaitForExit();
        Process.Start("c/main.out");
    }
}