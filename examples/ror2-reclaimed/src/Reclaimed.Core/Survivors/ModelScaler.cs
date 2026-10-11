using KinematicCharacterController;
using RoR2;
using UnityEngine;

namespace Reclaimed.Core.Survivors
{
    /// <summary>
    /// Rescales a body. Hurtboxes, hitboxes, child-locator points and particle effects live under the model
    /// transform, so scaling the model scales them consistently; root-level helpers (camera pivot, aim origin,
    /// model base offset) and the movement capsule are scaled explicitly.
    /// </summary>
    public static class ModelScaler
    {
        public static void Scale(GameObject body, float scale, string context)
        {
            if (body == null || Mathf.Approximately(scale, 1f) || scale <= 0f) return;
            ModelLocator locator = body.GetComponent<ModelLocator>();
            Transform model = locator != null ? locator.modelTransform : null;
            if (model == null)
            {
                Log.Warn(context + ": no model to scale.");
                return;
            }
            model.localScale *= scale;
            // Root children are the model base (its offset keeps the feet on the capsule bottom) and anchors such
            // as the camera pivot and aim origin; all of them move with the body's size.
            foreach (Transform child in body.transform) child.localPosition *= scale;
            foreach (ParticleSystem ps in model.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            CapsuleCollider capsule = body.GetComponent<CapsuleCollider>();
            if (capsule != null) SetCapsule(body, capsule.radius * scale, capsule.height * scale, capsule.center.y * scale);
        }

        /// <summary>Sets the movement capsule on both the collider and the KinematicCharacterMotor.</summary>
        public static void SetCapsule(GameObject body, float radius, float height, float yOffset)
        {
            height = Mathf.Max(height, radius * 2f);
            CapsuleCollider capsule = body.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.radius = radius;
                capsule.height = height;
                capsule.center = new Vector3(0f, yOffset, 0f);
                capsule.direction = 1;
            }
            KinematicCharacterMotor kcm = body.GetComponent<KinematicCharacterMotor>();
            if (kcm != null)
            {
                kcm.Capsule = capsule;
                kcm.CapsuleRadius = radius;
                kcm.CapsuleHeight = height;
                kcm.CapsuleYOffset = yOffset;
            }
        }

        /// <summary>
        /// Bounds of the model's meshes in <paramref name="space"/>'s local coordinates (e.g. the body root, to size
        /// a capsule or hurtbox when the boss has none). Built from shared mesh bounds and transforms, so it works on
        /// prefabs and clones that are inactive in the hierarchy, where Renderer.bounds is empty. Skinned meshes are
        /// measured in their bind pose. Returns false when the model has no meshes.
        /// </summary>
        public static bool TryModelBounds(Transform model, Transform space, out Bounds bounds)
        {
            bounds = default(Bounds);
            if (model == null) return false;
            if (space == null) space = model;
            Matrix4x4 toSpace = space.worldToLocalMatrix;
            bool any = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                SkinnedMeshRenderer skinned = r as SkinnedMeshRenderer;
                if (skinned != null) mesh = skinned.sharedMesh;
                else if (r is MeshRenderer)
                {
                    MeshFilter filter = r.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }
                if (mesh == null) continue;
                Encapsulate(ref bounds, ref any, mesh.bounds, toSpace * r.transform.localToWorldMatrix);
            }
            return any;
        }

        private static void Encapsulate(ref Bounds bounds, ref bool any, Bounds local, Matrix4x4 matrix)
        {
            Vector3 c = local.center;
            Vector3 e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    c.x + ((i & 1) != 0 ? e.x : -e.x),
                    c.y + ((i & 2) != 0 ? e.y : -e.y),
                    c.z + ((i & 4) != 0 ? e.z : -e.z));
                Vector3 p = matrix.MultiplyPoint3x4(corner);
                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else bounds.Encapsulate(p);
            }
        }
    }
}
