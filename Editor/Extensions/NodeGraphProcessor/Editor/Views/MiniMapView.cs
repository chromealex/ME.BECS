using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace ME.BECS.Extensions.GraphProcessor
{
	public class MiniMapView : MiniMap
	{
		Vector2				size;

		public MiniMapView(BaseGraphView baseGraphView) : base()
		{
			this.graphView = baseGraphView;
			SetPosition(new Rect(0, 0, 100, 100));
			size = new Vector2(100, 100);
		}
	}
}