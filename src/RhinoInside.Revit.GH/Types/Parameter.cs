using System;
using Grasshopper.Kernel.Types;
using RhinoInside.Revit.External.DB.Extensions;
using RhinoInside.Revit.External.UI.Extensions;
using DB = Autodesk.Revit.DB;

namespace RhinoInside.Revit.GH.Types
{
  using Kernel.Attributes;

  [Name("Parameter Key")]
  public class ParameterKey : Element
  {
    protected override Type ScriptVariableType => typeof(DB.ParameterElement);
    override public object ScriptVariable() => null;

    public ParameterKey() { }
    public ParameterKey(DB.Document doc, DB.ElementId id) : base(doc, id) { }
    public ParameterKey(DB.ParameterElement element) : base(element) { }

    new public static ParameterKey FromElementId(DB.Document doc, DB.ElementId id)
    {
      if (id.IsParameterId(doc))
        return new ParameterKey(doc, id);

      return null;
    }

    public override sealed bool CastFrom(object source)
    {
      if (base.CastFrom(source))
        return true;

      var document = Revit.ActiveDBDocument;
      var parameterId = DB.ElementId.InvalidElementId;

      if (source is ValueTuple<DB.Document, DB.ElementId> tuple)
      {
        (document, parameterId) = tuple;
      }
      else if (source is IGH_Goo goo)
      {
        if (source is IGH_Element element)
        {
          document = element.Document;
          parameterId = element.Id;
        }
        else source = goo.ScriptVariable();
      }

      switch (source)
      {
        case int integer:            parameterId = new DB.ElementId(integer); break;
        case DB.ElementId id:        parameterId = id; break;
        case DB.Parameter parameter: SetValue(parameter.Element.Document, parameter.Id); return true;
      }

      if (parameterId.TryGetBuiltInParameter(out var _))
      {
        SetValue(document, parameterId);
        return true;
      }

      return base.CastFrom(source);
    }

    public override bool CastTo<Q>(out Q target)
    {
      if (typeof(Q).IsAssignableFrom(typeof(GH_Guid)))
      {
        target = (Q) (object) (Document.GetElement(Id) as DB.SharedParameterElement)?.GuidValue;
        return true;
      }

      return base.CastTo<Q>(out target);
    }

    new class Proxy : Element.Proxy
    {
      protected new ParameterKey owner => base.owner as ParameterKey;

      public Proxy(ParameterKey o) : base(o) { (this as IGH_GooProxy).UserString = FormatInstance(); }

      public override bool IsParsable() => true;
      public override string FormatInstance()
      {
        int value = owner.Id?.IntegerValue ?? -1;
        if (Enum.IsDefined(typeof(DB.BuiltInParameter), value))
          return ((DB.BuiltInParameter) value).ToStringGeneric();

        return value.ToString();
      }
      public override bool FromString(string str)
      {
        if (Enum.TryParse(str, out DB.BuiltInParameter builtInParameter))
        {
          owner.SetValue(owner.Document ?? Revit.ActiveUIDocument.Document, new DB.ElementId(builtInParameter));
          return true;
        }

        return false;
      }

      #region Misc
      protected override bool IsValidId(DB.Document doc, DB.ElementId id) => id.IsParameterId(doc);
      public override Type ObjectType => IsBuiltIn ? typeof(DB.BuiltInParameter) : base.ObjectType;

      [System.ComponentModel.Description("BuiltIn parameter Id.")]
      public DB.BuiltInParameter? BuiltInId => owner.Id.TryGetBuiltInParameter(out var bip) ? bip : default;
      #endregion

      #region Definition
      const string Definition = "Definition";
      DB.ParameterElement parameter => owner.Value as DB.ParameterElement;

      [System.ComponentModel.Category(Definition), System.ComponentModel.Description("The Guid that identifies this parameter as a shared parameter.")]
      public Guid? Guid => (parameter as DB.SharedParameterElement)?.GuidValue;

      [System.ComponentModel.Category(Definition), System.ComponentModel.Description("Internal parameter data storage type.")]
      public DB.StorageType? StorageType => BuiltInId.HasValue ? Revit.ActiveDBDocument?.get_TypeOfStorage(BuiltInId.Value) : parameter?.GetDefinition()?.ParameterType.ToStorageType();

      [System.ComponentModel.Category(Definition), System.ComponentModel.Description("Visible in UI.")]
      public bool? Visible => parameter?.GetDefinition()?.Visible;

      [System.ComponentModel.Category(Definition), System.ComponentModel.Description("Whether or not the parameter values can vary across group members.")]
      public bool? VariesAcrossGroups => parameter?.GetDefinition()?.VariesAcrossGroups;

      [System.ComponentModel.Category(Definition)]
      public DB.ParameterType? Type => parameter?.GetDefinition()?.ParameterType;

      [System.ComponentModel.Category(Definition)]
      public DB.BuiltInParameterGroup? Group => parameter?.GetDefinition()?.ParameterGroup;
      #endregion
    }

    public override IGH_GooProxy EmitProxy() => new Proxy(this);

    public override string DisplayName
    {
      get
      {
        try
        {
          if (Id is object && Id.TryGetBuiltInParameter(out var builtInParameter))
            return DB.LabelUtils.GetLabelFor(builtInParameter) ?? base.DisplayName;
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException) { }

        return base.DisplayName;
      }
    }

    #region Properties
    public override string Name
    {
      get
      {
        try
        {
          if (Id is object && Id.TryGetBuiltInParameter(out var builtInParameter))
            return DB.LabelUtils.GetLabelFor(builtInParameter) ?? base.Name;
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException) { }

        return base.Name;
      }
      set
      {
        if (value is object && value != Name)
        {
          if (Id.IsBuiltInId())
            throw new InvalidOperationException($"BuiltIn paramater '{Name}' does not support assignment of a user-specified name.");

          base.Name = value;
        }
      }
    }
    #endregion
  }

  [Name("Parameter Value")]
  public class ParameterValue : ReferenceObject, IEquatable<ParameterValue>
  {
    #region System.Object
    public bool Equals(ParameterValue other)
    {
      if (other is null) return false;
      if (Value is DB.Parameter A && other.Value is DB.Parameter B)
      {
        if
        (
          A.Id.IntegerValue == B.Id.IntegerValue &&
          A.StorageType == B.StorageType &&
          A.HasValue == B.HasValue
        )
        {
          if (!Value.HasValue)
            return true;

          switch (Value.StorageType)
          {
            case DB.StorageType.None: return true;
            case DB.StorageType.Integer: return A.AsInteger() == B.AsInteger();
            case DB.StorageType.Double: return A.AsDouble() == B.AsDouble();
            case DB.StorageType.String: return A.AsString() == B.AsString();
            case DB.StorageType.ElementId: return A.AsElementId() == B.AsElementId();
          }
        }
      }

      return false;
    }
    public override bool Equals(object obj) => (obj is ElementId id) ? Equals(id) : base.Equals(obj);
    public override int GetHashCode()
    {
      int hashCode = 0;
      if (Value is DB.Parameter value)
      {
        hashCode ^= value.Id.GetHashCode();
        hashCode ^= value.StorageType.GetHashCode();

        if (value.HasValue)
        {
          switch (value.StorageType)
          {
            case DB.StorageType.Integer: hashCode ^= value.AsInteger().GetHashCode(); break;
            case DB.StorageType.Double: hashCode ^= value.AsDouble().GetHashCode(); break;
            case DB.StorageType.String: hashCode ^= value.AsString().GetHashCode(); break;
            case DB.StorageType.ElementId: hashCode ^= value.AsElementId().GetHashCode(); break;
          }
        }
      }

      return hashCode;
    }
    public override string ToString()
    {
      return Value.AsGoo()?.ToString();
    }
    #endregion

    #region IGH_Goo
    public override bool IsValid => base.IsValid && Value is object;
    public override bool CastFrom(object source)
    {
      if (source is DB.Parameter parameter)
      {
        base.Value = parameter;
        return true;
      }

      return false;
    }

    public override bool CastTo<Q>(out Q target)
    {
      if (typeof(Q).IsAssignableFrom(typeof(DB.Parameter)))
      {
        target = (Q) (object) Value;
        return true;
      }

      var goo = Value.AsGoo();
      if (goo is Q q)
      {
        target = q;
        return true;
      }

      return goo.CastTo(out target);
    }
    #endregion

    #region DocumentObject
    public new DB.Parameter Value => base.Value as DB.Parameter;

    public override string DisplayName
    {
      get
      {
        if (Value is DB.Parameter param)
          return param.Definition?.Name;

        return default;
      }
    }
    #endregion

    #region ReferenceObject
    public override DB.ElementId Id => Value?.Element.Id;
    #endregion

    public ParameterValue() { }
    public ParameterValue(DB.Parameter value) : base(value.Element.Document, value) { }
  }
}
