using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ME.BECS.Extensions.GraphProcessor
{
	using scg = System.Collections.Generic;
	[System.Serializable]
	public class CopyPasteHelper
	{
		public scg::List< JsonElement >	copiedNodes = new scg::List< JsonElement >();

		public scg::List< JsonElement >	copiedGroups = new scg::List< JsonElement >();
	
		public scg::List< JsonElement >	copiedEdges = new scg::List< JsonElement >();
	}
}