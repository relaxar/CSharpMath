using System;
using System.Collections.Generic;
using System.Linq;
using AngouriMath;

namespace CSharpMath {
  using Atom;
  using Atoms = Atom.Atoms;
  public static partial class Evaluation {
    enum Precedence {
      // Order matters
      Lowest,
      Bracket,
      AddSubtract,
      MultiplyDivide,
      FunctionApplication,
      UnaryPlusMinus,
      PercentDegree
    }
    public static MathList MathListFromEntity(Entity entity) =>
      LaTeXParser.MathListFromLaTeX(entity.Latexise())
      // CSharpMath must handle all LaTeX coming from MathS or a bug is present!
      .Match(list => list, e => throw new Structures.InvalidCodePathException(e));
    public static Structures.Result<Entity> MathListToEntity(MathList mathList) =>
      Transform(mathList.Clone(true))
      .Bind(entity =>
        entity is Entity e
        ? Structures.Result.Ok(e)
        : Structures.Result.Err("There is nothing to evaluate"));
    static Structures.Result<Entity?> Transform(MathList mathList) {
      int i = 0;
      return Transform(mathList, ref i, Precedence.Lowest);
    }
    static Structures.Result<Entity?> Transform(MathList mathList, ref int i, Precedence prec) {
      Entity? prevEntity = null;
      Entity? nextEntity;
      string? error;
      Precedence handlePrecendence;
      Func<Entity, Entity> handleUnary, handleFunction, handleFunctionInverse;
      Func<Entity, Entity, Entity> handleBinary;
      for (; i < mathList.Count; i++) {
        var atom = mathList[i];
        Entity? thisEntity;
        switch (atom) {
          case Atoms.Placeholder _:
            return "Placeholders should be filled";
          case Atoms.Number n:
            if (AngouriMath.Core.Number.TryParse(n.Nucleus, out var number)) {
              thisEntity = new NumberEntity(number);
              goto setEntity;
            } else return "Invalid number: " + n.Nucleus;
          case Atoms.Variable v:
            thisEntity = v.Nucleus switch
            {
              "e" => MathS.e,
              "π" => MathS.pi,
              "i" => new NumberEntity(MathS.i),
              _ when LaTeXSettings.CommandForAtom(atom) is string s => s,
              var name => new VariableEntity(name)
            };
            goto setEntity;
          case Atoms.Fraction f:
            Entity? numerator, denominator;
            (numerator, error) = Transform(f.Numerator);
            if (error != null) return error;
            if (numerator == null) return "Missing numerator";
            (denominator, error) = Transform(f.Denominator);
            if (error != null) return error;
            if (denominator == null) return "Missing denominator";
            thisEntity = numerator / denominator;
            goto setEntity;
          case Atoms.Radical r:
            Entity? degree, radicand;
            (degree, error) = Transform(r.Degree).Bind(degree => degree is null ? 0.5 : 1 / degree);
            if (error != null) return error;
            (radicand, error) = Transform(r.Radicand);
            if (error != null) return error;
            if (radicand == null) return "Missing radicand";
            thisEntity = MathS.Pow(radicand, degree);
            goto setEntity;
          case Atoms.Open { Nucleus: "(" }:
            i++;
            (thisEntity, error) = Transform(mathList, ref i, Precedence.Bracket);
            if (error != null) return error;
            if (thisEntity == null)
              return "Missing )";
            goto setEntity;
          case Atoms.Close { Nucleus: ")", Superscript: var super }:
            if (prevEntity == null)
              return "Missing math before )";
            switch (prec) {
              case Precedence.Lowest:
                return "Missing (";
              case Precedence.Bracket:
                if (super.IsNonEmpty()) {
                  (degree, error) = Transform(super);
                  if (error != null) return error;
                  prevEntity = MathS.Pow(prevEntity, degree);
                }
                return prevEntity;
              default:
                i--;
                return prevEntity;
            }
          case Atoms.Inner { LeftBoundary: { Nucleus: "(" }, InnerList: var inner, RightBoundary: { Nucleus: ")" } }:
            (thisEntity, error) = Transform(inner);
            if (error != null) return error;
            if (thisEntity == null) return "Missing math between ()";
            goto setEntity;
          case Atoms.UnaryOperator { Nucleus: "+" }:
            handlePrecendence = Precedence.UnaryPlusMinus;
            handleUnary = e => +e;
            goto handleUnary;
          case Atoms.UnaryOperator { Nucleus: "\u2212" }:
            handlePrecendence = Precedence.UnaryPlusMinus;
            handleUnary = e => -e;
            goto handleUnary;
          case Atoms.LargeOperator { Nucleus: "sin" }:
            handleFunction = MathS.Sin;
            handleFunctionInverse = MathS.Arcsin;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "cos" }:
            handleFunction = MathS.Cos;
            handleFunctionInverse = MathS.Arccos;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "tan" }:
            handleFunction = MathS.Tan;
            handleFunctionInverse = MathS.Arctan;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "cot" }:
            handleFunction = MathS.Cotan;
            handleFunctionInverse = MathS.Arccotan;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "sec" }:
            handleFunction = MathS.Sec;
            handleFunctionInverse = MathS.Arcsec;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "csc" }:
            handleFunction = MathS.Cosec;
            handleFunctionInverse = MathS.Arccosec;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "arcsin" }:
            handleFunction = MathS.Arcsin;
            handleFunctionInverse = MathS.Sin;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "arccos" }:
            handleFunction = MathS.Arccos;
            handleFunctionInverse = MathS.Cos;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "arctan" }:
            handleFunction = MathS.Arctan;
            handleFunctionInverse = MathS.Tan;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "arccot" }:
            handleFunction = MathS.Arccotan;
            handleFunctionInverse = MathS.Cotan;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "arcsec" }:
            handleFunction = MathS.Arcsec;
            handleFunctionInverse = MathS.Sec;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "arccsc" }:
            handleFunction = MathS.Arccosec;
            handleFunctionInverse = MathS.Cosec;
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "log", Subscript: var @base }:
            Entity? logBase;
            (logBase, error) = Transform(@base);
            if (error != null) return error;
            logBase ??= new NumberEntity(10);
            handleFunction = arg => MathS.Log(arg, logBase);
            handleFunctionInverse = arg => MathS.Pow(logBase, arg);
            goto handleFunction;
          case Atoms.LargeOperator { Nucleus: "ln" }:
            handleFunction = MathS.Ln;
            handleFunctionInverse = arg => MathS.Pow(MathS.e, arg);
            goto handleFunction;
          case Atoms.BinaryOperator { Nucleus: "+" }:
            handlePrecendence = Precedence.AddSubtract;
            handleBinary = (a, b) => a + b;
            goto handleBinary;
          case Atoms.BinaryOperator { Nucleus: "\u2212" }:
            handlePrecendence = Precedence.AddSubtract;
            handleBinary = (a, b) => a - b;
            goto handleBinary;
          case Atoms.BinaryOperator { Nucleus: "*" }:
          case Atoms.BinaryOperator { Nucleus: "×" }:
          case Atoms.BinaryOperator { Nucleus: "·" }:
            handlePrecendence = Precedence.MultiplyDivide;
            handleBinary = (a, b) => a * b;
            goto handleBinary;
          case Atoms.BinaryOperator { Nucleus: "÷" }:
          case Atoms.Ordinary { Nucleus: "/" }:
            handlePrecendence = Precedence.MultiplyDivide;
            handleBinary = (a, b) => a / b;
            goto handleBinary;
          case Atoms.Ordinary { Nucleus: "%" }:
            handlePrecendence = Precedence.PercentDegree;
            handleUnary = x => x / 100;
            goto handleUnaryPostfix;
          case Atoms.Ordinary { Nucleus: "°" }:
            handlePrecendence = Precedence.PercentDegree;
            handleUnary = x => x * MathS.pi / 180;
            goto handleUnaryPostfix;
          case Atoms.Space _:
          case Atoms.Ordinary { Nucleus: var nucleus } when string.IsNullOrWhiteSpace(nucleus):
            continue;
          default:
            return $"Unsupported {atom.TypeName} {atom.Nucleus}";
            handleFunction:
            if (atom.Superscript.EqualsList(new MathList(new Atoms.UnaryOperator("\u2212"), new Atoms.Number("1")))) {
              atom.Superscript.Clear();
              handleFunction = handleFunctionInverse;
            }
            i++;
            MathList? bracketArgument = null;
            int open = -1;
            // Steal the exponent of the following argument!
            // e.g. sin(x)^2 -> sin^2(x) and sin^2(x)^3 -> sin^(2*3)(x)
            // but sin x^2 remains as-is
            for (int levelsDeep = 0; i < mathList.Count; i++)
              switch (mathList[i]) {
                case Atoms.Space _:
                case Atoms.Ordinary { Nucleus: var nucleus } when string.IsNullOrWhiteSpace(nucleus):
                  break;
                case Atoms.Inner inner:
                  var superscript = inner.Superscript;
                  bracketArgument = inner.InnerList;
                  goto stealExponent;
                case Atoms.Open _:
                  if (levelsDeep == 0) open = i;
                  levelsDeep++;
                  break;
                case Atoms.Close close:
                  levelsDeep--;
                  if (levelsDeep == 0) {
                    if (open == -1) return "Missing argument for " + atom.Nucleus;
                    else bracketArgument = mathList.Slice(open + 1, i - open - 1);
                    superscript = close.Superscript;
                    goto stealExponent;
                  }
                  break;
                default:
                  if (levelsDeep == 0)
                    goto exitFor;
                  break;
                  stealExponent:
                  _ = bracketArgument; // Ensure assignment
                  if (levelsDeep > 0)
                    break;
                  if (atom.Superscript.IsNonEmpty() && superscript.IsNonEmpty()) {
                    var originalSuperscript = new Atoms.Inner(new Boundary("("), new MathList(), new Boundary(")"));
                    originalSuperscript.InnerList.Append(atom.Superscript);
                    var newSuperscript = new Atoms.Inner(new Boundary("("), new MathList(), new Boundary(")"));
                    newSuperscript.InnerList.Append(superscript);

                    atom.Superscript.Clear();
                    superscript.Clear();
                    atom.Superscript.Add(originalSuperscript);
                    atom.Superscript.Add(LaTeXSettings.Times);
                    atom.Superscript.Add(newSuperscript);
                  } else {
                    atom.Superscript.Append(superscript);
                    superscript.Clear();
                  }
                  goto exitFor;
              }
            exitFor:
            (nextEntity, error) =
              bracketArgument == null
              ? Transform(mathList, ref i, Precedence.FunctionApplication)
              : Transform(bracketArgument);
            if (error != null) return error;
            if (nextEntity == null) return "Missing argument for " + atom.Nucleus;
            thisEntity = handleFunction(nextEntity);
            goto setEntity;

            handleUnary:
            i++;
            (nextEntity, error) = Transform(mathList, ref i, handlePrecendence);
            if (error != null) return error;
            if (nextEntity == null) return "Missing right operand for " + atom.Nucleus;
            thisEntity = handleUnary(nextEntity);
            goto setEntity;

            handleBinary:
            if (prevEntity is null) {
              // No previous entity, treat as unary operator (happens for 1---2)
              if (atom is Atoms.BinaryOperator b) {
                mathList[i] = b.ToUnaryOperator();
              } else {
                mathList[i] = new Atoms.UnaryOperator(atom.Nucleus);
                mathList[i].Superscript.Append(atom.Superscript);
                mathList[i].Subscript.Append(atom.Subscript);
              }
              i--;
              continue;
            }
            if (prec < handlePrecendence) {
              i++;
              (nextEntity, error) = Transform(mathList, ref i, handlePrecendence);
              if (error != null) return error;
              if (nextEntity == null) return "Missing right operand for " + atom.Nucleus;
              thisEntity = handleBinary(prevEntity, nextEntity);
              prevEntity = null; // We used up the previous entity, don't keep it
              goto setEntity;
            } else {
              i--;
              return prevEntity;
            }

            handleUnaryPostfix:
            if (prevEntity == null) return "Missing left operand for " + atom.Nucleus;
            if (prec < handlePrecendence) {
              thisEntity = handleUnary(prevEntity);
              prevEntity = null; // We used up prevEntity
              goto setEntity;
            } else {
              i--;
              return prevEntity;
            }

            setEntity:
            Entity? exponent;
            (exponent, error) = Transform(atom.Superscript);
            if (error != null) return error;
            if (exponent != null)
              thisEntity = MathS.Pow(thisEntity, exponent);
            prevEntity = prevEntity is { } ? prevEntity * thisEntity : thisEntity;
            break;
        }
      }
      if (prec == Precedence.Bracket) return "Missing )";
      return prevEntity;
    }
  }
}