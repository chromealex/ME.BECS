﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ME.BECS.Extensions.GraphProcessor
{
	using scg = System.Collections.Generic;
	/// <summary>
	/// Group the selected node when created
	/// </summary>
	[System.Serializable]
	public class Group
	{
		public string			title;
		public Color			color = new Color(0, 0, 0, 0.5f);
		public Rect				position;
        public Vector2          size;
        public bool enabled = true;
        public string GUID;
        public bool transparent = false;
		
		/// <summary>
		/// Store the GUIDs of the node in the group
		/// </summary>
		/// <typeparam name="string">GUID of a node</typeparam>
		/// <returns></returns>
		public scg::List< string >	innerNodeGUIDs = new scg::List< string >();

		// For serialization loading
        public Group() {}

		/// <summary>
		/// Create a new group with a title and a position
		/// </summary>
		/// <param name="title"></param>
		/// <param name="position"></param>
        public Group(string title, Vector2 position)
		{
			this.title = title;
            this.position.position = position;
		}

		/// <summary>
		/// Called when the Group is created
		/// </summary>
        public virtual void OnCreated()
        {
            size = new Vector2(400, 200);
            position.size = size;
            GUID = System.Guid.NewGuid().ToString();
        }
	}
}