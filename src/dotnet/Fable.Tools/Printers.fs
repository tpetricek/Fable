module Fable.Tools.Printers

open System
open System.IO
open Microsoft.FSharp.Compiler.SourceCodeServices
open Newtonsoft.Json
open Fable
open Fable.AST

let attribsOfSymbol (s:FSharpSymbol) =
    let tryOr f def =
        try f() with _ -> def
    [ match s with
        | :? FSharpField as v ->
            yield "field"
            if v.IsCompilerGenerated then yield "compgen"
            if v.IsDefaultValue then yield "default"
            if v.IsMutable then yield "mutable"
            if v.IsVolatile then yield "volatile"
            if v.IsStatic then yield "static"
            if v.IsLiteral then yield sprintf "%A" v.LiteralValue.Value

        | :? FSharpEntity as v ->
            v.TryFullName |> ignore // check there is no failure here
            match v.BaseType with
            | Some t when t.HasTypeDefinition && t.TypeDefinition.TryFullName.IsSome ->
                yield sprintf "inherits %s" t.TypeDefinition.FullName
            | _ -> ()
            if v.IsNamespace then yield "namespace"
            if v.IsFSharpModule then yield "module"
            if v.IsByRef then yield "byref"
            if v.IsClass then yield "class"
            if v.IsDelegate then yield "delegate"
            if v.IsEnum then yield "enum"
            if v.IsFSharpAbbreviation then yield "abbrev"
            if v.IsFSharpExceptionDeclaration then yield "exception"
            if v.IsFSharpRecord then yield "record"
            if v.IsFSharpUnion then yield "union"
            if v.IsInterface then yield "interface"
            if v.IsMeasure then yield "measure"
            // if v.IsProvided then yield "provided"
            // if v.IsStaticInstantiation then yield "static_inst"
            // if v.IsProvidedAndErased then yield "erased"
            // if v.IsProvidedAndGenerated then yield "generated"
            if v.IsUnresolved then yield "unresolved"
            if v.IsValueType then yield "valuetype"

        | :? FSharpMemberOrFunctionOrValue as v ->
            yield "owner: " + (tryOr (fun () -> v.EnclosingEntity.CompiledName) "<unknown>")
            if v.IsActivePattern then yield "active_pattern"
            if v.IsDispatchSlot then yield "dispatch_slot"
            if v.IsModuleValueOrMember && not v.IsMember then yield "val"
            if v.IsMember then yield "member"
            if v.IsProperty then yield "property"
            if v.IsExtensionMember then yield "extension_member"
            if v.IsPropertyGetterMethod then yield "property_getter"
            if v.IsPropertySetterMethod then yield "property_setter"
            if v.IsEvent then yield "event"
            if v.EventForFSharpProperty.IsSome then yield "property_event"
            if v.IsEventAddMethod then yield "event_add"
            if v.IsEventRemoveMethod then yield "event_remove"
            if v.IsTypeFunction then yield "type_func"
            if v.IsCompilerGenerated then yield "compiler_gen"
            if v.IsImplicitConstructor then yield "implicit_ctor"
            if v.IsMutable then yield "mutable"
            if v.IsOverrideOrExplicitInterfaceImplementation then yield "override_impl"
            if not v.IsInstanceMember then yield "static"
            if v.IsInstanceMember && not v.IsInstanceMemberInCompiledCode && not v.IsExtensionMember then yield "funky"
            if v.IsExplicitInterfaceImplementation then yield "interface_impl"
            yield sprintf "%A" v.InlineAnnotation
            // if v.IsConstructorThisValue then yield "ctorthis"
            // if v.IsMemberThisValue then yield "this"
            // if v.LiteralValue.IsSome then yield "literal"
        | _ -> () ]

let rec printFSharpDecls prefix decls = seq {
    let mutable i = 0
    for decl in decls do
        i <- i + 1
        match decl with
        | FSharpImplementationFileDeclaration.Entity (e, sub) ->
            yield sprintf "%s%i) ENTITY: %s %A" prefix i e.CompiledName (attribsOfSymbol e)
            if not (Seq.isEmpty e.Attributes) then
                yield sprintf "%sattributes: %A" prefix (Seq.toList e.Attributes)
            if not (Seq.isEmpty e.DeclaredInterfaces) then
                yield sprintf "%sinterfaces: %A" prefix (Seq.toList e.DeclaredInterfaces)
            yield ""
            yield! printFSharpDecls (prefix + "\t") sub
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue (meth, args, body) ->
            yield sprintf "%s%i) METHOD: %s %A" prefix i meth.CompiledName (attribsOfSymbol meth)
            yield sprintf "%stype: %A" prefix meth.FullType
            yield sprintf "%sargs: %A" prefix args
            // if not meth.IsCompilerGenerated then
            yield sprintf "%sbody: %A" prefix body
            yield ""
        | FSharpImplementationFileDeclaration.InitAction (expr) ->
            yield sprintf "%s%i) ACTION" prefix i
            yield sprintf "%s%A" prefix expr
            yield ""
}

let printFableDecls decls = seq {
    for decl in decls do
        yield sprintf "%A" decl
}

let printAst outDir (proj: FSharpCheckProjectResults) =
    for f in proj.AssemblyContents.ImplementationFiles do
        let target =
            let name = System.IO.Path.GetFileNameWithoutExtension(f.FileName)
            Path.Combine(outDir, name + ".fs.ast")
        Log.logVerbose(sprintf "Print AST %s" target)
        printFSharpDecls "" f.Declarations
        |> fun lines -> System.IO.File.WriteAllLines(target, lines)
        // printFableDecls fableFile.Declarations
        // |> fun lines -> System.IO.File.WriteAllLines(Path.Combine(outDir, name + ".fable.ast"), lines)
