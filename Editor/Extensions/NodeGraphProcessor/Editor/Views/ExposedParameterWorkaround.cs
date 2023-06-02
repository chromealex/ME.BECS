using UnityEngine;
using System;
using System.Collections.Generic;

namespace ME.BECS.Extensions.GraphProcessor
{
    using scg = System.Collections.Generic;
    [Serializable]
    public class ExposedParameterWorkaround : ScriptableObject
    {
        [SerializeReference]
        public scg::List<ExposedParameter>   parameters = new scg::List<ExposedParameter>();
        public BaseGraph                graph;
    }
}