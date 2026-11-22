using System.Collections.Generic;
using UnityEngine;
using GS = Gravexel.Core;

namespace Gravexel.UnityHost
{
    /// <summary>
    /// Draws the game's sprite/text commands with pooled SpriteRenderers and TextMeshes.
    /// The camera is orthographic with size 540, so one world unit = one virtual pixel (1080 tall).
    /// </summary>
    public sealed class UnityRenderer : GS.IRenderer
    {
        sealed class TexInfo { public UnityEngine.Sprite Sprite; public float Aspect; }
        sealed class LayerData { public GS.DrawCmd[] Cmds; public readonly List<LayerInstance> Instances = new List<LayerInstance>(); public int Used; }
        sealed class LayerInstance { public Transform Root; public SpriteRenderer[] Renderers; public int Base = -1; }

        readonly Transform _root;
        readonly Material _alpha, _add;
        readonly Font _titleFont, _uiFont;
        readonly Dictionary<int, TexInfo> _tex = new Dictionary<int, TexInfo>();
        readonly Dictionary<int, LayerData> _layers = new Dictionary<int, LayerData>();
        readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
        readonly List<TextMesh> _texts = new List<TextMesh>();
        int _spriteUsed, _textUsed, _order;
        const int FontPx = 64;

        public UnityRenderer(Transform root)
        {
            _root = root;
            _alpha = new Material(Shader.Find("Gravexel/Alpha"));
            _add = new Material(Shader.Find("Gravexel/Additive"));
            _titleFont = Resources.Load<Font>("Fonts/Audiowide-Regular");
            _uiFont = Resources.Load<Font>("Fonts/Rajdhani-Bold");
        }

        public float ViewW { get { return 1080f * Screen.width / Mathf.Max(1, Screen.height); } }
        public float ViewH { get { return 1080f; } }

        public void BeginFrame()
        {
            _spriteUsed = 0; _textUsed = 0; _order = -32000;
            foreach (var l in _layers.Values) l.Used = 0;
        }

        public void EndFrame()
        {
            for (int i = _spriteUsed; i < _sprites.Count; i++) if (_sprites[i].enabled) _sprites[i].enabled = false;
            for (int i = _textUsed; i < _texts.Count; i++) if (_texts[i].gameObject.activeSelf) _texts[i].gameObject.SetActive(false);
            foreach (var l in _layers.Values)
                for (int i = 0; i < l.Instances.Count; i++)
                {
                    bool on = i < l.Used;
                    if (l.Instances[i].Root.gameObject.activeSelf != on) l.Instances[i].Root.gameObject.SetActive(on);
                }
        }

        Vector3 ToWorld(float x, float y) { return new Vector3(x - ViewW * 0.5f, 540f - y, 0f); }

        public void RegisterTexture(int id, GS.PixelImage img)
        {
            var t = new Texture2D(img.W, img.H, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[img.W * img.H];
            for (int y = 0; y < img.H; y++)
                for (int x = 0; x < img.W; x++)
                {
                    int s = ((img.H - 1 - y) * img.W + x) * 4; // Unity rows go bottom-up
                    px[y * img.W + x] = new Color32(img.Rgba[s], img.Rgba[s + 1], img.Rgba[s + 2], img.Rgba[s + 3]);
                }
            t.SetPixels32(px); t.Apply(false, true);
            var sp = UnityEngine.Sprite.Create(t, new UnityEngine.Rect(0, 0, img.W, img.H), new Vector2(0.5f, 0.5f), img.W, 0, SpriteMeshType.FullRect);
            _tex[id] = new TexInfo { Sprite = sp, Aspect = img.H / (float)img.W };
        }

        SpriteRenderer NextSprite()
        {
            if (_spriteUsed >= _sprites.Count)
            {
                var go = new GameObject("s"); go.transform.SetParent(_root, false);
                _sprites.Add(go.AddComponent<SpriteRenderer>());
            }
            var sr = _sprites[_spriteUsed++];
            if (!sr.enabled) sr.enabled = true;
            return sr;
        }

        void Setup(SpriteRenderer sr, TexInfo t, Transform tr, Vector3 pos, float w, float h, float rot, GS.Col c, GS.Blend b)
        {
            sr.sprite = t.Sprite;
            sr.sharedMaterial = b == GS.Blend.Add ? _add : _alpha;
            sr.color = new Color(c.R, c.G, c.B, c.A);
            tr.localPosition = pos;
            tr.localRotation = Quaternion.Euler(0, 0, -rot * Mathf.Rad2Deg);
            tr.localScale = new Vector3(w, h / t.Aspect, 1);
        }

        public void Background(GS.Col top, GS.Col bottom)
        {
            Sprite(GS.Tx.Pixel, ViewW / 2, 540, ViewW + 4, 1084, 0, GS.Col.Lerp(top, bottom, 0.5f), GS.Blend.Alpha);
        }

        public void Sprite(int tex, float cx, float cy, float w, float h, float rot, GS.Col c, GS.Blend b)
        {
            TexInfo t; if (!_tex.TryGetValue(tex, out t) || c.A <= 0.002f) return;
            var sr = NextSprite();
            Setup(sr, t, sr.transform, ToWorld(cx, cy), w, h, rot, c, b);
            sr.sortingOrder = _order++;
        }

        Font FontFor(GS.FontId f) { return f == GS.FontId.Title ? _titleFont : _uiFont; }

        public void Text(string text, float x, float y, float size, GS.Col c, GS.Align align, GS.FontId font)
        {
            if (string.IsNullOrEmpty(text) || c.A <= 0.002f) return;
            if (_textUsed >= _texts.Count)
            {
                var go = new GameObject("t"); go.transform.SetParent(_root, false);
                var tmNew = go.AddComponent<TextMesh>();
                tmNew.fontSize = FontPx;
                _texts.Add(tmNew);
            }
            var tm = _texts[_textUsed++];
            if (!tm.gameObject.activeSelf) tm.gameObject.SetActive(true);
            var f = FontFor(font);
            if (tm.font != f) { tm.font = f; tm.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
            tm.text = text;
            tm.characterSize = size * 10f / FontPx;
            tm.anchor = align == GS.Align.Left ? TextAnchor.MiddleLeft : align == GS.Align.Center ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
            tm.alignment = align == GS.Align.Left ? TextAlignment.Left : align == GS.Align.Center ? TextAlignment.Center : TextAlignment.Right;
            tm.color = new Color(c.R, c.G, c.B, c.A);
            tm.transform.localPosition = ToWorld(x, y + size * 0.06f);
            tm.GetComponent<MeshRenderer>().sortingOrder = _order++;
        }

        public float MeasureText(string text, float size, GS.FontId font)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var f = FontFor(font);
            f.RequestCharactersInTexture(text, FontPx);
            float w = 0; CharacterInfo ci;
            foreach (char ch in text) if (f.GetCharacterInfo(ch, out ci, FontPx)) w += ci.advance;
            return w * size / FontPx;
        }

        public void BuildStaticLayer(int layerId, GS.DrawCmd[] cmds, int count, float width, float height)
        {
            LayerData old;
            if (_layers.TryGetValue(layerId, out old))
                foreach (var inst in old.Instances) Object.Destroy(inst.Root.gameObject);
            var arr = new GS.DrawCmd[count];
            System.Array.Copy(cmds, arr, count);
            _layers[layerId] = new LayerData { Cmds = arr };
        }

        LayerInstance CreateInstance(LayerData l)
        {
            var root = new GameObject("layer").transform; root.SetParent(_root, false);
            var list = new List<SpriteRenderer>();
            foreach (var d in l.Cmds)
            {
                TexInfo t; if (!_tex.TryGetValue(d.Tex, out t)) continue;
                var go = new GameObject("c"); go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                Setup(sr, t, go.transform, new Vector3(d.X, -d.Y, 0), d.W, d.H, d.Rot, d.Color, d.Blend);
                list.Add(sr);
            }
            return new LayerInstance { Root = root, Renderers = list.ToArray() };
        }

        public void DrawStaticLayer(int layerId, float x, float y)
        {
            LayerData l; if (!_layers.TryGetValue(layerId, out l)) return;
            if (l.Used >= l.Instances.Count) l.Instances.Add(CreateInstance(l));
            var inst = l.Instances[l.Used++];
            if (!inst.Root.gameObject.activeSelf) inst.Root.gameObject.SetActive(true);
            inst.Root.localPosition = ToWorld(x, y);
            if (inst.Base != _order)
            {
                for (int i = 0; i < inst.Renderers.Length; i++) inst.Renderers[i].sortingOrder = _order + i;
                inst.Base = _order;
            }
            _order += inst.Renderers.Length;
        }

        public void Vignette(float strength)
        {
            // corners darkened with four large soft glows in black
            float w = ViewW;
            var c = new GS.Col(0, 0, 0, 0.55f * strength);
            Sprite(GS.Tx.Glow, 0, 0, w * 0.9f, 900, 0, c, GS.Blend.Alpha);
            Sprite(GS.Tx.Glow, w, 0, w * 0.9f, 900, 0, c, GS.Blend.Alpha);
            Sprite(GS.Tx.Glow, 0, 1080, w * 0.9f, 900, 0, c, GS.Blend.Alpha);
            Sprite(GS.Tx.Glow, w, 1080, w * 0.9f, 900, 0, c, GS.Blend.Alpha);
        }
    }
}
