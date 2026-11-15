using System.Collections.Generic;
using UnityEngine;

namespace MissionDesign.Characters
{
    public enum Outfit { Engineer, AssemblyEngineer, CleanRoom, Astronaut }

    /// <summary>
    /// Vestuario según el ambiente sobre el avatar (mallas avaturn_look/shoes/hair/body):
    /// ingeniero aeroespacial (camisa del color del equipo + auriculares), ingeniero de ensamblaje (casco amarillo),
    /// traje de sala limpia (mono blanco, capucha y mascarilla) y traje espacial naranja de lanzamiento con casco.
    /// Los accesorios se construyen con primitivas y se fijan al hueso de la cabeza.
    /// </summary>
    public sealed class OutfitController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Outfit outfit = Outfit.Engineer;
        [Tooltip("Color de la camisa del ingeniero (multiplica la textura original).")]
        [SerializeField] private Color shirtTint = Color.white;
        [SerializeField] private Color hairTint = Color.white;
        [SerializeField] private Color skinTint = Color.white;
        [SerializeField] private bool headset;

        private Renderer look, shoes, hair, body;
        private Material lookOriginal, shoesOriginal;
        private Material lookTinted, suitWhite, suitOrange, bootsBlack, bootsWhite;
        private readonly Dictionary<string, GameObject> accessories = new Dictionary<string, GameObject>();
        private bool built;

        public Outfit Current => outfit;

        public void Configure(Animator a, Color shirt, Color hairColor, Color skin, bool withHeadset, Outfit initial)
        {
            animator = a;
            shirtTint = shirt; hairTint = hairColor; skinTint = skin; headset = withHeadset; outfit = initial;
        }

        private void Start()
        {
            Build();
            Set(outfit, force: true);
        }

        private void Build()
        {
            if (built) return;
            built = true;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("look")) look = r;
                else if (n.Contains("shoes")) shoes = r;
                else if (n.Contains("hair")) hair = r;
                else if (n.Contains("body")) body = r;
            }
            if (look != null)
            {
                lookOriginal = look.sharedMaterial;
                lookTinted = new Material(lookOriginal) { color = shirtTint };
                suitWhite = Plain(lookOriginal, new Color(0.94f, 0.96f, 1f));
                suitOrange = Plain(lookOriginal, new Color(0.98f, 0.42f, 0.08f));
            }
            if (shoes != null)
            {
                shoesOriginal = shoes.sharedMaterial;
                bootsBlack = Plain(shoesOriginal, new Color(0.08f, 0.08f, 0.09f));
                bootsWhite = Plain(shoesOriginal, new Color(0.92f, 0.94f, 0.97f));
            }
            if (hair != null && hairTint != Color.white) hair.material.color = hairTint;
            if (body != null && skinTint != Color.white) body.material.color = skinTint;

            var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var chest = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            if (head == null) return;
            Vector3 up = transform.up, fwd = transform.forward, right = transform.right;
            Vector3 c = head.position + up * 0.09f;   // centro aproximado del cráneo

            var white = Solid(new Color(0.95f, 0.96f, 0.98f));
            var dark = Solid(new Color(0.08f, 0.08f, 0.1f));
            var visor = Solid(new Color(0.55f, 0.42f, 0.18f), metallic: 0.9f, smooth: 0.95f);
            var yellow = Solid(new Color(1f, 0.78f, 0.05f));
            var blue = Solid(new Color(0.35f, 0.62f, 0.95f));
            var nasaBlue = Solid(new Color(0.1f, 0.22f, 0.55f));

            var set = new GameObject("Acc_Headset");
            Part(set, head, PrimitiveType.Cube, c + up * 0.115f, new Vector3(0.2f, 0.02f, 0.035f), dark);
            Part(set, head, PrimitiveType.Sphere, c - right * 0.095f, new Vector3(0.04f, 0.07f, 0.07f), dark);
            Part(set, head, PrimitiveType.Sphere, c + right * 0.095f, new Vector3(0.04f, 0.07f, 0.07f), dark);
            Part(set, head, PrimitiveType.Cube, c - right * 0.075f + fwd * 0.06f - up * 0.06f, new Vector3(0.012f, 0.012f, 0.12f), dark);
            accessories["headset"] = set;

            var hardHat = new GameObject("Acc_HardHat");
            Part(hardHat, head, PrimitiveType.Sphere, c + up * 0.06f, new Vector3(0.25f, 0.16f, 0.27f), yellow);
            Part(hardHat, head, PrimitiveType.Cylinder, c + up * 0.03f + fwd * 0.02f, new Vector3(0.3f, 0.006f, 0.33f), yellow);
            accessories["hardhat"] = hardHat;

            var hood = new GameObject("Acc_CleanHood");
            Part(hood, head, PrimitiveType.Sphere, c - fwd * 0.045f + up * 0.01f, new Vector3(0.25f, 0.29f, 0.25f), white);
            Part(hood, head, PrimitiveType.Cube, c + fwd * 0.095f - up * 0.06f, new Vector3(0.11f, 0.06f, 0.03f), blue);
            accessories["hood"] = hood;

            var helmet = new GameObject("Acc_Helmet");
            Part(helmet, head, PrimitiveType.Sphere, c - fwd * 0.02f, new Vector3(0.34f, 0.35f, 0.35f), white);
            Part(helmet, head, PrimitiveType.Sphere, c + fwd * 0.11f + up * 0.01f, new Vector3(0.26f, 0.2f, 0.16f), visor);
            Part(helmet, head, PrimitiveType.Cylinder, head.position - up * 0.07f, new Vector3(0.26f, 0.03f, 0.26f), nasaBlue);
            accessories["helmet"] = helmet;

            if (chest != null)
            {
                var badge = new GameObject("Acc_Badge");
                Part(badge, chest, PrimitiveType.Cube, chest.position + fwd * 0.13f + right * 0.06f + up * 0.06f, new Vector3(0.05f, 0.07f, 0.01f), white);
                accessories["badge"] = badge;
            }
        }

        public void Set(Outfit next, bool force = false)
        {
            if (!built) Build();
            if (!force && next == outfit) return;
            outfit = next;
            bool engineer = next == Outfit.Engineer || next == Outfit.AssemblyEngineer;
            if (look != null)
                look.sharedMaterial = engineer ? lookTinted : next == Outfit.CleanRoom ? suitWhite : suitOrange;
            if (shoes != null)
                shoes.sharedMaterial = engineer ? shoesOriginal : next == Outfit.CleanRoom ? bootsWhite : bootsBlack;
            if (hair != null) hair.enabled = engineer;
            Show("headset", next == Outfit.Engineer && headset);
            Show("hardhat", next == Outfit.AssemblyEngineer);
            Show("hood", next == Outfit.CleanRoom);
            Show("helmet", next == Outfit.Astronaut);
            Show("badge", engineer);
        }

        private void Show(string key, bool visible)
        {
            if (accessories.TryGetValue(key, out var go) && go != null) go.SetActive(visible);
        }

        private static void Part(GameObject group, Transform bone, PrimitiveType type, Vector3 worldPos, Vector3 size, Material mat)
        {
            if (group.transform.parent == null) group.transform.SetParent(bone, true);
            var p = GameObject.CreatePrimitive(type);
            Object.Destroy(p.GetComponent<Collider>());
            p.layer = bone.gameObject.layer;
            p.transform.SetParent(group.transform, true);
            p.transform.position = worldPos;
            p.transform.rotation = bone.root.rotation;
            p.transform.localScale = Vector3.one;
            var ls = p.transform.lossyScale;
            p.transform.localScale = new Vector3(size.x / ls.x, size.y / ls.y, size.z / ls.z);
            p.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Material Plain(Material template, Color c)
        {
            var m = new Material(template) { color = c, mainTexture = null };
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
            return m;
        }

        private static Material Solid(Color c, float metallic = 0f, float smooth = 0.4f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { color = c };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }
    }
}
