// Decompiled with JetBrains decompiler
// Type: UnityEditor.Experimental.GraphView.GridBackground
// Assembly: UnityEditor.GraphViewModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: DB3332CF-0219-47AE-B7EB-63E0E8325729
// Assembly location: /Applications/Unity/Hub/Editor/2022.3.39f1/Unity.app/Contents/Managed/UnityEngine/UnityEditor.GraphViewModule.dll
// XML documentation location: /Applications/Unity/Hub/Editor/2022.3.39f1/Unity.app/Contents/Managed/UnityEngine/UnityEditor.GraphViewModule.xml

using System;
using UnityEngine;
using UnityEngine.UIElements;

#nullable disable
namespace ME.BECS.Extensions.GraphProcessor
{
  /// <summary>
  ///   <para>Default GraphView background.</para>
  /// </summary>
  public class GridBackground : ImmediateModeElement
  {
    private static CustomStyleProperty<float> s_SpacingProperty = new CustomStyleProperty<float>("--spacing");
    private static CustomStyleProperty<int> s_ThickLinesProperty = new CustomStyleProperty<int>("--thick-lines");
    private static CustomStyleProperty<Color> s_LineColorProperty = new CustomStyleProperty<Color>("--line-color");
    private static CustomStyleProperty<Color> s_ThickLineColorProperty = new CustomStyleProperty<Color>("--thick-line-color");
    private static CustomStyleProperty<Color> s_GridBackgroundColorProperty = new CustomStyleProperty<Color>("--grid-background-color");
    private static readonly float s_DefaultSpacing = 50f;
    private static readonly int s_DefaultThickLines = 10;
    private static readonly Color s_DefaultLineColor = new Color(0.0f, 0.0f, 0.0f, 0.18f);
    private static readonly Color s_DefaultThickLineColor = new Color(0.0f, 0.0f, 0.0f, 0.38f);
    private static readonly Color s_DefaultGridBackgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f);
    private float m_Spacing = GridBackground.s_DefaultSpacing;
    private float m_SpacingY = GridBackground.s_DefaultSpacing;
    private int m_ThickLines = GridBackground.s_DefaultThickLines;
    private Color m_LineColor = GridBackground.s_DefaultLineColor;
    private Color m_ThickLineColor = GridBackground.s_DefaultThickLineColor;
    private Color m_GridBackgroundColor = GridBackground.s_DefaultGridBackgroundColor;
    private VisualElement m_Container;

    public Vector2 offset;

    public float opacity = 1f;
    
    public ref float spacing => ref this.m_Spacing;
    public ref float spacingY => ref this.m_SpacingY;

    private int thickLines => this.m_ThickLines;

    private Color lineColor => new Color(this.m_LineColor.r, this.m_LineColor.g, this.m_LineColor.b, this.m_LineColor.a * this.opacity);

    private Color thickLineColor => this.m_ThickLineColor;

    private Color gridBackgroundColor => this.m_GridBackgroundColor;

    /// <summary>
    ///   <para>GridBackground's constructor.</para>
    /// </summary>
    public GridBackground()
    {
      this.pickingMode = PickingMode.Ignore;
      this.StretchToParentSize();
      this.RegisterCallback<CustomStyleResolvedEvent>(new EventCallback<CustomStyleResolvedEvent>(this.OnCustomStyleResolved));
    }

    private Vector3 Clip(Rect clipRect, Vector3 _in)
    {
      if ((double) _in.x < (double) clipRect.xMin)
        _in.x = clipRect.xMin;
      if ((double) _in.x > (double) clipRect.xMax)
        _in.x = clipRect.xMax;
      if ((double) _in.y < (double) clipRect.yMin)
        _in.y = clipRect.yMin;
      if ((double) _in.y > (double) clipRect.yMax)
        _in.y = clipRect.yMax;
      return _in;
    }

    private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
    {
      float num1 = 0.0f;
      int num2 = 0;
      Color clear1 = Color.clear;
      Color clear2 = Color.clear;
      Color clear3 = Color.clear;
      ICustomStyle customStyle = e.customStyle;
      if (customStyle.TryGetValue(GridBackground.s_SpacingProperty, out num1))
        this.m_Spacing = num1;
      if (customStyle.TryGetValue(GridBackground.s_SpacingProperty, out num1))
        this.m_SpacingY = num1;
      if (customStyle.TryGetValue(GridBackground.s_ThickLinesProperty, out num2))
        this.m_ThickLines = this.thickLines;
      if (customStyle.TryGetValue(GridBackground.s_ThickLineColorProperty, out clear1))
        this.m_ThickLineColor = clear1;
      if (customStyle.TryGetValue(GridBackground.s_LineColorProperty, out clear2))
        this.m_LineColor = clear2;
      if (!customStyle.TryGetValue(GridBackground.s_GridBackgroundColorProperty, out clear3))
        return;
      this.m_GridBackgroundColor = clear3;
    }
    
    static Material lineMaterial;
    static void CreateLineMaterial()
    {
      if (!lineMaterial)
      {
        // Unity has a built-in shader that is useful for drawing
        // simple colored things.
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        // Turn on alpha blending
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        // Turn backface culling off
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        // Turn off depth writes
        lineMaterial.SetInt("_ZWrite", 0);
      }
    }

    protected override void ImmediateRepaint()
    {
      CreateLineMaterial();
      // Apply the line material
      lineMaterial.SetPass(0);
      this.m_Container = this.parent is UnityEditor.Experimental.GraphView.GraphView parent ? parent.contentViewContainer : this.parent;
      Rect layout1 = this.parent.layout;
      layout1.x = 0.0f;
      layout1.y = 0.0f;
      Vector3 vector3_1 = default;
      ref Vector3 local = ref vector3_1;
      double magnitude1 = (double) this.m_Container.transform.matrix.GetColumn(0).magnitude;
      Matrix4x4 matrix = this.m_Container.transform.matrix;
      double magnitude2 = (double) matrix.GetColumn(1).magnitude;
      matrix = this.m_Container.transform.matrix;
      double magnitude3 = (double) matrix.GetColumn(2).magnitude;
      local = new Vector3((float) magnitude1, (float) magnitude2, (float) magnitude3);
      matrix = this.m_Container.transform.matrix;
      Vector4 column = matrix.GetColumn(3);
      Rect layout2 = this.m_Container.layout;
      //UnityEditor.HandleUtility.ApplyWireMaterial();
      GL.Begin(GL.QUADS);
      GL.Color(this.gridBackgroundColor);
      GL.Vertex(new Vector3(layout1.x, layout1.y));
      GL.Vertex(new Vector3(layout1.xMax, layout1.y));
      GL.Vertex(new Vector3(layout1.xMax, layout1.yMax));
      GL.Vertex(new Vector3(layout1.x, layout1.yMax));
      GL.End();
      Vector3 vector3_2 = new Vector3(layout1.x, layout1.y, 0.0f);
      Vector3 vector3_3 = new Vector3(layout1.x, layout1.height, 0.0f);
      Matrix4x4 matrix4x4 = Matrix4x4.TRS((Vector3) column, Quaternion.identity, Vector3.one);
      vector3_2 = matrix4x4.MultiplyPoint(vector3_2) + (Vector3)this.offset;
      vector3_3 = matrix4x4.MultiplyPoint(vector3_3) + (Vector3)this.offset;
      vector3_2.x += layout2.x * vector3_1.x;
      vector3_2.y += layout2.y * vector3_1.y;
      vector3_3.x += layout2.x * vector3_1.x;
      vector3_3.y += layout2.y * vector3_1.y;
      float x = vector3_2.x;
      float y = vector3_2.y;
      vector3_2.x = (float) ((double) vector3_2.x % ((double) this.spacing * (double) vector3_1.x) - (double) this.spacing * (double) vector3_1.x);
      vector3_3.x = vector3_2.x;
      vector3_2.y = layout1.y;
      vector3_3.y = layout1.y + layout1.height;
      while ((double) vector3_2.x < (double) layout1.width)
      {
        vector3_2.x += this.spacing * vector3_1.x;
        vector3_3.x += this.spacing * vector3_1.x;
        GL.Begin(GL.LINES);
        GL.Color(this.lineColor);
        GL.Vertex(this.Clip(layout1, vector3_2));
        GL.Vertex(this.Clip(layout1, vector3_3));
        GL.End();
      }
      float num1 = this.spacing * (float) this.thickLines;
      for (vector3_2.x = vector3_3.x = (float) ((double) x % ((double) num1 * (double) vector3_1.x) - (double) num1 * (double) vector3_1.x); (double) vector3_2.x < (double) layout1.width + (double) num1; vector3_3.x += this.spacing * vector3_1.x * (float) this.thickLines)
      {
        GL.Begin(GL.LINES);
        GL.Color(this.thickLineColor);
        GL.Vertex(this.Clip(layout1, vector3_2));
        GL.Vertex(this.Clip(layout1, vector3_3));
        GL.End();
        vector3_2.x += this.spacing * vector3_1.x * (float) this.thickLines;
      }
      vector3_2 = new Vector3(layout1.x, layout1.y, 0.0f);
      vector3_3 = new Vector3(layout1.x + layout1.width, layout1.y, 0.0f);
      vector3_2.x += layout2.x * vector3_1.x;
      vector3_2.y += layout2.y * vector3_1.y;
      vector3_3.x += layout2.x * vector3_1.x;
      vector3_3.y += layout2.y * vector3_1.y;
      vector3_2 = matrix4x4.MultiplyPoint(vector3_2);
      vector3_3 = matrix4x4.MultiplyPoint(vector3_3);
      vector3_2.y = vector3_3.y = (float) ((double) vector3_2.y % ((double) this.spacingY * (double) vector3_1.y) - (double) this.spacingY * (double) vector3_1.y);
      vector3_2.x = layout1.x;
      vector3_3.x = layout1.width;
      while ((double) vector3_2.y < (double) layout1.height)
      {
        vector3_2.y += this.spacingY * vector3_1.y;
        vector3_3.y += this.spacingY * vector3_1.y;
        GL.Begin(GL.LINES);
        GL.Color(this.lineColor);
        GL.Vertex(this.Clip(layout1, vector3_2));
        GL.Vertex(this.Clip(layout1, vector3_3));
        GL.End();
      }
      float num2 = this.spacingY * (float) this.thickLines;
      for (vector3_2.y = vector3_3.y = (float) ((double) y % ((double) num2 * (double) vector3_1.y) - (double) num2 * (double) vector3_1.y); (double) vector3_2.y < (double) layout1.height + (double) num2; vector3_3.y += this.spacingY * vector3_1.y * (float) this.thickLines)
      {
        GL.Begin(GL.LINES);
        GL.Color(this.thickLineColor);
        GL.Vertex(this.Clip(layout1, vector3_2));
        GL.Vertex(this.Clip(layout1, vector3_3));
        GL.End();
        vector3_2.y += this.spacingY * vector3_1.y * (float) this.thickLines;
      }
    }
  }
}
