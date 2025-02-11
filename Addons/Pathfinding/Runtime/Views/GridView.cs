#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

using UnityEngine;

namespace ME.BECS.Pathfinding.Views {
    
    public class GridView : ME.BECS.Views.EntityView {

        private static readonly int centerPos = Shader.PropertyToID("_ObjPos");
        private static readonly int @params = Shader.PropertyToID("_Params");
        private static readonly int gridTex = Shader.PropertyToID("_GridTex");
        private static readonly int _gridSize = Shader.PropertyToID("_GridSize");
        private static readonly int isEnabled = Shader.PropertyToID("_IsEnabled");
        private static readonly int isValid = Shader.PropertyToID("_IsValid");
        private static readonly int gridOffset = Shader.PropertyToID("_GridOffset");

        public Material material;
        private uint2 gridSize;
        private float2 viewWorldSize;
        private float4 objPos;
        public float2 offset;

        protected override void OnInitialize(in EntRO ent) {
            
            var grid = ent.World.GetSystem<ShowBuildingGridSystem>();
            this.gridSize = grid.gridSize;
            
        }

        protected override void ApplyState(in ME.BECS.EntRO ent) {

            var grid = ent.World.GetSystem<ShowBuildingGridSystem>();
            var objPos = ent.GetAspect<ME.BECS.Transforms.TransformAspect>().position;
            var objSize = ent.Read<ME.BECS.Units.UnitQuadSizeComponent>().size;
            var halfOffset = ME.BECS.Pathfinding.GraphUtils.SizeOffset(objSize);
            this.viewWorldSize = this.gridSize;// + halfOffset;
            //var halfSize = this.viewWorldSize * 0.5f;
            //objPos.x = math.clamp(objPos.x, (int)halfSize.x * root.nodeSize - halfOffset.x, (int)(root.width * root.chunkWidth - halfSize.x * root.nodeSize + halfOffset.x));
            //objPos.z = math.clamp(objPos.z, (int)halfSize.y * root.nodeSize - halfOffset.y, (int)(root.height * root.chunkHeight - halfSize.y * root.nodeSize + halfOffset.y));
            objPos.x += halfOffset.x;
            objPos.z += halfOffset.y;
            this.objPos = new float4(objPos, 0f);
            this.material.SetTexture(gridTex, grid.GetTexture());
            this.material.SetFloat(isEnabled, ent.Has<IsShowGridComponent>() == true ? 1f : 0f);
            this.material.SetVector(_gridSize, new Vector4((float)this.viewWorldSize.x, (float)this.viewWorldSize.y, 0f, 0f));
            this.material.SetFloat(isValid, ent.Has<PlaceholderInvalidTagComponent>() == true ? 0f : 1f);
            this.material.SetVector(gridOffset, new Vector4(0.5f, 0.5f, objSize.x % 2 != 0 ? 1f : -1f, objSize.y % 2 != 0 ? 1f : -1f));

        }

        protected override void OnUpdate(in EntRO ent, float dt) { 
            
            var invScaleX = 1f / this.viewWorldSize.x;
            var invScaleY = 1f / this.viewWorldSize.y;
            var x = this.offset.x - this.viewWorldSize.x * 0.5f;
            var y = this.offset.y - this.viewWorldSize.y * 0.5f;
            
            var p = new Vector4((float)(-x * invScaleX), (float)(-y * invScaleY), (float)invScaleX, 0f);
            this.material.SetVector(centerPos, (Vector4)this.objPos);
            this.material.SetVector(@params, p);
            
        }

    }

}