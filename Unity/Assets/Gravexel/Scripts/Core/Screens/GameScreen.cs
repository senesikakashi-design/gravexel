using System;

namespace Gravexel.Core
{
    /// <summary>Gameplay: runs the physics, effects, HUD, pause menu and level-complete panel.</summary>
    public sealed class GameScreen : Screen, IStepEvents
    {
        enum State { Ready, Playing, Dead, Paused, Complete }

        const int IdPause = 100, IdResume = 101, IdRestart = 102, IdSettings = 103, IdLevels = 104, IdMenu = 105,
            IdNext = 110, IdRetry = 111, IdCLevels = 112;
        const float TrailStep = 1f / 60f;

        readonly int _number;
        LevelInfo _info; LevelData L; Theme T;
        readonly WorldView _view = new WorldView();
        readonly Particles _fx = new Particles(1600);
        Skin _skin;

        State _state = State.Ready, _resumeState = State.Ready;
        float _stateT, _acc, _time, _runTime, _flipBuffer, _freeze;
        PlayerState _p; float _prevX, _prevY;
        bool[] _orbTaken; int _orbs, _deaths, _flips;
        bool _flippedThisStep, _wasGrounded, _started;

        int _cp = -1, _cpPending = -1; PlayerState _cpState; bool _hasCp;
        float _camX, _camY, _shake, _visRot, _rotTarget, _sqX = 1, _sqY = 1, _flash, _orbCombo, _orbComboT;
        readonly float[] _trX = new float[16], _trY = new float[16], _trR = new float[16];
        int _trHead; float _trAcc;
        int _savedStars;

        // completion
        int _score, _runStars; bool _newBest, _newTime; string _unlock = ""; float _shownScore; int _starsPopped;

        public GameScreen(int levelNumber) { _number = levelNumber; }

        // read-only state for automated tests
        public string StateName { get { return _state.ToString(); } }
        public int RunStars { get { return _p.Flags & Physics.StarMask; } }
        public int Deaths { get { return _deaths; } }
        public int OrbsCollected { get { return _orbs; } }
        public int Score { get { return _score; } }

        public override void Enter()
        {
            _info = App.Level(_number) ?? App.Levels[0];
            L = new LevelData(_info);
            T = Themes.For(_info.World);
            _skin = Skins.All[Mathx.Clamp(App.Save.SelectedSkin, 0, Skins.All.Length - 1)];
            _view.Build(App.R, L, T);
            App.Ui.Accent = T.Edge; App.Ui.Accent2 = T.Accent;
            _orbTaken = new bool[L.Orbs.Count];
            _savedStars = App.Save.Get(_number).StarBits;
            App.Save.LastLevel = _number;
            App.PlayMusic(Sfx.MusicWorld1 + Mathx.Clamp(_info.World - 1, 0, 5));
            ResetRun(false);
            _camX = TargetCamX(); _camY = TargetCamY();
        }

        void ResetRun(bool sound)
        {
            _p = Physics.Spawn(L);
            _prevX = _p.X; _prevY = _p.Y;
            for (int i = 0; i < _orbTaken.Length; i++) _orbTaken[i] = false;
            _orbs = 0; _deaths = 0; _flips = 0; _runTime = 0; _time = 0;
            _cp = -1; _cpPending = -1; _hasCp = false; _started = false;
            _visRot = _rotTarget = 0;
            _fx.Clear();
            ResetTrail();
            SetState(State.Ready);
            if (sound) App.PlaySfx(Sfx.Respawn, 0.6f);
        }

        void ResetTrail() { for (int i = 0; i < _trX.Length; i++) { _trX[i] = _p.X; _trY[i] = _p.Y; _trR[i] = _visRot; } }

        void SetState(State s) { _state = s; _stateT = 0; }

        float ViewTilesW { get { return App.R.ViewW / WorldView.TilePx; } }
        float ViewTilesH { get { return App.R.ViewH / WorldView.TilePx; } }

        float TargetCamX()
        {
            float vw = ViewTilesW;
            if (L.W <= vw) return L.W / 2f;
            float t = Mathx.Lerp(_prevX, _p.X, _acc / Physics.Dt) + _p.Dir * 2.5f;
            return Mathx.Clamp(t, vw / 2, L.W - vw / 2);
        }

        float TargetCamY()
        {
            float vh = ViewTilesH;
            if (L.H <= vh) return L.H / 2f;
            return Mathx.Clamp(_p.Y, vh / 2, L.H - vh / 2);
        }

        Rect PauseRect { get { return new Rect(App.R.ViewW - 110, 26, 80, 80); } }

        // ---------------- update ----------------

        public override void Update(float dt)
        {
            var input = App.In;
            _stateT += dt;
            // hazards keep moving while dead/ready (after the first start); physics advances time while playing
            if (_state == State.Dead || _state == State.Complete || (_state == State.Ready && _started)) _time += dt;

            if (input.Pause && (_state == State.Playing || _state == State.Ready || _state == State.Dead))
            {
                _resumeState = _state; SetState(State.Paused); App.PlaySfx(Sfx.Click, 0.6f);
                App.Ui.Focus = IdResume;
                return;
            }
            if (_state == State.Paused)
            {
                if (input.Pause || input.Back) Resume();
                return;
            }
            if (input.Restart && _state != State.Complete) { ResetRun(true); return; }

            bool tap = input.PointerPressed && !PauseRect.Contains(input.PointerX, input.PointerY);
            bool press = input.Action || tap;

            switch (_state)
            {
                case State.Ready:
                    if (press && _stateT > 0.15f)
                    {
                        SetState(State.Playing); _acc = 0; _started = true;
                        App.PlaySfx(Sfx.Start, 0.7f);
                        _fx.Burst(_p.X, _p.Y, 14, _skin.TrailAt(App.Time), 5f, 0.35f, 0.5f);
                    }
                    break;
                case State.Playing:
                    if (press) _flipBuffer = 0.12f;
                    if (_freeze > 0) { _freeze -= dt; break; }
                    _acc += dt;
                    int guard = 0;
                    while (_acc >= Physics.Dt && guard++ < 24)
                    {
                        _acc -= Physics.Dt;
                        _prevX = _p.X; _prevY = _p.Y;
                        _flippedThisStep = false;
                        _wasGrounded = _p.Grounded;
                        Physics.Step(L, ref _p, _time, _flipBuffer > 0, this);
                        if (_flippedThisStep) _flipBuffer = 0;
                        _flipBuffer -= Physics.Dt;
                        _time += Physics.Dt; _runTime += Physics.Dt;
                        if (_cpPending >= 0 && _p.Grounded && _p.Alive)
                        {
                            _cp = _cpPending; _cpPending = -1; _cpState = _p; _hasCp = true;
                            App.PlaySfx(Sfx.Checkpoint, 0.8f);
                            var c = L.Checkpoints[_cp];
                            _fx.Burst(c.X, c.Y, 24, T.Accent, 6f, 0.35f, 0.7f);
                        }
                        if (!_p.Alive || _p.Won) break;
                    }
                    if (_acc > Physics.Dt * 4) _acc = 0;
                    break;
                case State.Dead:
                    if (_stateT > 0.8f) Respawn();
                    break;
                case State.Complete:
                    UpdateComplete(dt);
                    break;
            }

            // visuals
            _rotTarget = _p.G > 0 ? Snap(_rotTarget, 0) : Snap(_rotTarget, 1);
            _visRot = Mathx.Damp(_visRot, _rotTarget, 16f, dt);
            _sqX = Mathx.Damp(_sqX, 1, 12, dt); _sqY = Mathx.Damp(_sqY, 1, 12, dt);
            _shake = Math.Max(0, _shake - dt * 2.5f);
            _flash = Math.Max(0, _flash - dt * 3f);
            _orbComboT -= dt; if (_orbComboT <= 0) _orbCombo = 0;
            _fx.Update(dt);
            _camX = Mathx.Damp(_camX, TargetCamX(), 5f, dt);
            _camY = Mathx.Damp(_camY, TargetCamY(), 5f, dt);

            _trAcc += dt;
            while (_trAcc >= TrailStep)
            {
                _trAcc -= TrailStep;
                _trHead = (_trHead + 1) % _trX.Length;
                _trX[_trHead] = RenderX; _trY[_trHead] = RenderY; _trR[_trHead] = _visRot;
            }
            if (_state == State.Playing && _p.Alive && App.Time % 0.05f < dt)
                _fx.Spawn(RenderX - _p.Dir * 0.3f, RenderY + _p.G * 0.3f, -_p.Dir * 1.5f, -_p.G * 0.5f, 0.35f, 0.18f, _skin.TrailAt(App.Time).WithA(0.7f));
        }

        /// <summary>Keeps the visual rotation target at the closest multiple of PI with the right parity (0 = upright, 1 = upside down).</summary>
        float Snap(float current, int parity)
        {
            float k = (float)Math.Round(current / Mathx.PI);
            if (((int)Math.Abs(k) & 1) != parity) k += _p.Dir >= 0 ? 1 : -1;
            return k * Mathx.PI;
        }

        float RenderX { get { return _state == State.Playing ? Mathx.Lerp(_prevX, _p.X, Mathx.Clamp01(_acc / Physics.Dt)) : _p.X; } }
        float RenderY { get { return _state == State.Playing ? Mathx.Lerp(_prevY, _p.Y, Mathx.Clamp01(_acc / Physics.Dt)) : _p.Y; } }

        void Resume() { SetState(_resumeState); _stateT = 1f; App.PlaySfx(Sfx.Back, 0.5f); }

        void Respawn()
        {
            byte keep = (byte)(_p.Flags & (Physics.StarMask | Physics.KeyBit));
            if (_hasCp) { _p = _cpState; _p.Flags = (byte)((_cpState.Flags & Physics.ToggleBit) | keep); _p.VY = 0; _p.Alive = true; _p.Won = false; }
            else { _p = Physics.Spawn(L); _p.Flags = keep; }
            _p.InPortal = false; _p.InSwitch = false;
            _prevX = _p.X; _prevY = _p.Y;
            _visRot = _rotTarget = _p.G > 0 ? 0 : Mathx.PI;
            ResetTrail();
            _fx.Burst(_p.X, _p.Y, 26, _skin.ColorAt(App.Time), -5f, 0.3f, 0.4f);
            App.PlaySfx(Sfx.Respawn, 0.6f);
            SetState(State.Ready);
        }

        // ---------------- physics events ----------------

        public void OnFlip()
        {
            _flippedThisStep = true; _flips++;
            App.PlaySfx(Sfx.Flip, 0.75f, _p.G < 0 ? 1.15f : 0.95f);
            _sqX = 0.75f; _sqY = 1.3f;
            _fx.Burst(_p.X, _p.Y - _p.G * 0.35f, 8, _skin.TrailAt(App.Time), 4f, 0.25f, 0.35f);
        }

        public void OnLand(float speed)
        {
            float k = Mathx.Clamp01(speed / Physics.MaxFall);
            _sqX = 1f + 0.35f * k; _sqY = 1f - 0.3f * k;
            App.PlaySfx(Sfx.Land, 0.3f + 0.5f * k, 0.9f + 0.2f * k);
            for (int i = 0; i < 6; i++)
                _fx.Spawn(_p.X + _fx.Rng.Range(-0.3f, 0.3f), _p.Y + _p.G * 0.37f, _fx.Rng.Range(-2.5f, 2.5f), -_p.G * _fx.Rng.Range(0.5f, 2f), 0.35f, 0.16f, T.Edge.WithA(0.8f));
            if (k > 0.6f) _shake = Math.Max(_shake, 0.12f);
        }

        public void OnTurn()
        {
            App.PlaySfx(Sfx.Turn, 0.4f);
            _sqX = 0.8f; _sqY = 1.15f;
            _fx.Burst(_p.X + _p.Dir * -0.4f, _p.Y, 5, T.Edge, 3f, 0.18f, 0.3f);
        }

        public void OnOrb(int index)
        {
            if (_orbTaken[index]) return;
            _orbTaken[index] = true; _orbs++;
            _orbCombo = Math.Min(_orbCombo + 1, 12); _orbComboT = 0.6f;
            App.PlaySfx(Sfx.Orb, 0.55f, 1f + _orbCombo * 0.06f);
            var o = L.Orbs[index];
            _fx.Burst(o.X, o.Y, 10, T.Edge, 4f, 0.22f, 0.4f);
        }

        public void OnStar(int index)
        {
            App.PlaySfx(Sfx.Star, 0.8f);
            var s = L.Stars[index];
            _fx.Burst(s.X, s.Y, 30, WorldView.StarCol, 7f, 0.35f, 0.8f);
            _flash = 0.35f;
        }

        public void OnKey()
        {
            App.PlaySfx(Sfx.Key, 0.7f);
            App.PlaySfx(Sfx.Gate, 0.5f);
            foreach (var k in L.Keys) _fx.Burst(k.X, k.Y, 24, WorldView.KeyCol, 6f, 0.3f, 0.7f);
            _shake = Math.Max(_shake, 0.15f);
        }

        public void OnSwitch()
        {
            App.PlaySfx(Sfx.Switch, 0.7f);
            foreach (var s in L.Switches) _fx.Burst(s.X, s.Y, 14, T.Accent, 4f, 0.25f, 0.4f);
        }

        public void OnPortal(byte kind)
        {
            App.PlaySfx(Sfx.Portal, 0.6f, kind == TileKind.PortalUp ? 1.2f : kind == TileKind.PortalDown ? 0.85f : 1f);
            Col c = kind == TileKind.PortalUp ? WorldView.PortalUpCol : kind == TileKind.PortalDown ? WorldView.PortalDownCol : WorldView.PortalFlipCol;
            _fx.Burst(_p.X, _p.Y, 16, c, 5f, 0.3f, 0.5f);
            _sqX = 0.8f; _sqY = 1.25f;
        }

        public void OnCheckpoint(int index)
        {
            if (index == _cp || index == _cpPending) return;
            if (_hasCp && index < _cp) return;
            _cpPending = index;
        }

        public void OnDeath(int cause)
        {
            _deaths++;
            App.Save.TotalDeaths++;
            App.PlaySfx(Sfx.Death, 0.85f);
            if (App.Save.Settings.ScreenShake) _shake = 0.5f;
            _fx.Burst(_p.X, _p.Y, 40, _skin.ColorAt(App.Time), 9f, 0.35f, 0.9f);
            _fx.Burst(_p.X, _p.Y, 18, Col.White, 6f, 0.2f, 0.5f);
            for (int i = 0; i < 10; i++)
                _fx.Spawn(_p.X, _p.Y, _fx.Rng.Range(-6f, 6f), _fx.Rng.Range(-6f, 6f), 0.8f, 0.22f, _skin.ColorAt(App.Time), Tx.BodyFor(_skin.Shape), Blend.Alpha, 1.5f, _p.G * 18f, _fx.Rng.Range(-10f, 10f));
            SetState(State.Dead);
        }

        public void OnWin()
        {
            App.PlaySfx(Sfx.Win, 0.85f);
            _fx.Burst(_p.X, _p.Y, 60, T.Accent, 10f, 0.4f, 1.2f);
            _fx.Burst(_p.X, _p.Y, 30, Col.White, 6f, 0.25f, 0.8f);
            FinishLevel();
            SetState(State.Complete);
        }

        // ---------------- completion ----------------

        void FinishLevel()
        {
            _runStars = _p.Flags & Physics.StarMask;
            int starCount = 0; for (int i = 0; i < 3; i++) if ((_runStars & (1 << i)) != 0) starCount++;
            float timeBonus = _info.Par > 0 ? Math.Max(0, _info.Par - _runTime) * 20f : 0;
            _score = Math.Max(100, _orbs * 10 + starCount * 150 + 500 + (int)timeBonus - _deaths * 25);

            var save = App.Save;
            var rec = save.Get(_number);
            int beforeStars = save.TotalStars;
            rec.Completed = true;
            rec.StarBits |= _runStars;
            _newBest = _score > rec.BestScore; if (_newBest) rec.BestScore = _score;
            _newTime = rec.BestTime <= 0 || _runTime < rec.BestTime; if (_newTime) rec.BestTime = _runTime;
            save.TotalOrbs += _orbs; save.TotalFlips += _flips;
            if (App.Level(_number + 1) != null) save.LastLevel = _number + 1;

            _unlock = "";
            int total = save.TotalStars;
            for (int i = 0; i < Skins.All.Length; i++)
                if (Skins.All[i].StarsNeeded <= total && !save.SeenSkins.Contains(i))
                {
                    save.SeenSkins.Add(i);
                    if (_unlock.Length == 0) _unlock = Skins.All[i].Name;
                }
            App.SaveNow();
            _shownScore = 0; _starsPopped = 0;
            App.Ui.Focus = IdNext;
        }

        void UpdateComplete(float dt)
        {
            float t = _stateT;
            int shouldPop = t < 0.6f ? 0 : Math.Min(3, (int)((t - 0.6f) / 0.28f) + 1);
            while (_starsPopped < shouldPop)
            {
                bool got = (_runStars & (1 << _starsPopped)) != 0;
                if (got) App.PlaySfx(Sfx.Star, 0.6f, 1f + _starsPopped * 0.12f);
                _starsPopped++;
            }
            if (t > 1.2f) _shownScore = Mathx.Approach(_shownScore, _score, dt * Math.Max(800, _score * 1.5f));
            if (t > 1.6f && _unlock.Length > 0 && t - dt <= 1.6f) App.PlaySfx(Sfx.Unlock, 0.7f);
        }

        void NextLevel()
        {
            var next = App.Level(_number + 1);
            if (next != null) App.GoTo(new GameScreen(_number + 1));
            else App.GoTo(new LevelSelectScreen(_info.World, true));
        }

        // ---------------- drawing ----------------

        public override void Draw(bool interactive)
        {
            var r = App.R; var ui = App.Ui;
            float S = WorldView.TilePx;
            float sx = 0, sy = 0;
            if (_shake > 0 && App.Save.Settings.ScreenShake)
            {
                float k = _shake * _shake * 28f;
                sx = Mathx.Sin(App.Time * 91f) * k; sy = Mathx.Cos(App.Time * 77f) * k;
            }
            float ox = (float)Math.Round(-_camX * S + r.ViewW / 2 + sx), oy = (float)Math.Round(-_camY * S + r.ViewH / 2 + sy);

            App.Backdrop.Draw(r, T, _camX * S, App.Time, (_camY - L.H / 2f) * -S, true);
            r.DrawStaticLayer(WorldView.LayerLevel, ox, oy - WorldView.LayerPad);
            _view.DrawDynamic(r, ox, oy, _time, _p.Flags, _orbTaken, _p.Flags & Physics.StarMask, _savedStars, _cp);

            // player
            if (_state != State.Dead)
            {
                float px = RenderX * S + ox, py = RenderY * S + oy;
                for (int i = 1; i < _trX.Length; i++)
                {
                    int idx = (_trHead - i + _trX.Length * 2) % _trX.Length;
                    float k = 1f - i / (float)_trX.Length;
                    float tx = _trX[idx] * S + ox, ty = _trY[idx] * S + oy;
                    r.Sprite(Tx.BodyFor(_skin.Shape), tx, ty, 48 * k, 48 * k, _trR[idx], _skin.TrailAt(App.Time - i * 0.02f).WithA(0.22f * k), Blend.Add);
                }
                float idle = _state == State.Ready ? Mathx.Sin(App.Time * 4) * 0.04f : 0;
                WorldView.DrawCharacter(r, _skin, px, py, 50, _visRot, _p.Dir, App.Time, _sqX + idle, _sqY - idle, 1f, Mathx.Cos(_visRot));
            }
            _fx.Draw(r, 0, 0, S, ox, oy);
            if (_flash > 0) r.Sprite(Tx.Pixel, r.ViewW / 2, r.ViewH / 2, r.ViewW, r.ViewH, 0, WorldView.StarCol.WithA(_flash * 0.12f), Blend.Add);
            r.Vignette(0.55f);

            DrawHud(interactive && _state != State.Paused && _state != State.Complete);
            if (_state == State.Paused) DrawPause(interactive);
            if (_state == State.Complete) DrawComplete(interactive);
        }

        static string Clock(float t)
        {
            int m = (int)(t / 60); float s = t - m * 60;
            return m.ToString("00") + ":" + s.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        void DrawHud(bool interactive)
        {
            var r = App.R; var ui = App.Ui;
            float W = r.ViewW;
            // left: level title
            r.Sprite(Tx.FadeH, 260, 66, 520, 112, 0, new Col(0.02f, 0.03f, 0.08f, 0.75f), Blend.Alpha);
            r.Sprite(Tx.Pixel, 6, 66, 6, 70, 0, T.Edge, Blend.Alpha);
            r.Text("WORLD " + _info.World + "  ·  LEVEL " + LevelLabel(), 34, 44, 26, T.Edge, Align.Left, FontId.Ui);
            r.Text(_info.Name.ToUpperInvariant(), 34, 84, 40, Col.White, Align.Left, FontId.Title);

            // centre: stars
            ui.Stars(W / 2, 62, 46, _p.Flags & Physics.StarMask, 62, App.Time);

            // right: orbs, timer, deaths, pause
            float x = W - 150;
            r.Sprite(Tx.FadeH, W - 330, 66, 660, 112, Mathx.PI, new Col(0.02f, 0.03f, 0.08f, 0.75f), Blend.Alpha);
            if (App.Save.Settings.ShowTimer) r.Text(Clock(_runTime), x - 20, 50, 36, Col.White, Align.Right, FontId.Ui);
            string orbText = _orbs + " / " + L.Orbs.Count;
            float ow = r.MeasureText(orbText, 30, FontId.Ui);
            float ox = x - 210 - ow;
            r.Sprite(Tx.Glow, ox - 22, 50, 50, 50, 0, T.Edge.WithA(0.6f), Blend.Add);
            r.Sprite(Tx.Ring, ox - 22, 50, 22, 22, 0, T.Edge, Blend.Alpha);
            r.Text(orbText, ox, 50, 30, Col.White, Align.Left, FontId.Ui);
            r.Text("DEATHS " + _deaths, x - 20, 92, 24, Col.White.WithA(0.6f), Align.Right, FontId.Ui);
            // The pause button is mouse/touch only: it must never take keyboard focus,
            // otherwise Space/Enter (the flip keys) would "press" it and open the pause menu.
            var pr = PauseRect;
            bool hover = interactive && pr.Contains(App.In.PointerX, App.In.PointerY);
            if (hover) r.Sprite(Tx.Glow, pr.CX, pr.CY, pr.W * 2, pr.H * 2, 0, T.Edge.WithA(0.3f), Blend.Add);
            r.Sprite(Tx.Circle, pr.CX, pr.CY, pr.W, pr.H, 0, new Col(0.03f, 0.05f, 0.12f, 0.8f), Blend.Alpha);
            r.Sprite(Tx.Ring, pr.CX, pr.CY, pr.W, pr.H, 0, (hover ? T.Edge : Col.White).WithA(hover ? 1f : 0.4f), Blend.Alpha);
            r.Sprite(Tx.IconPause, pr.CX, pr.CY, pr.W * 0.5f, pr.H * 0.5f, 0, Col.White.WithA(interactive ? 1f : 0.6f), Blend.Alpha);
            if (hover && App.In.PointerPressed)
            {
                _resumeState = _state; SetState(State.Paused); App.Ui.Focus = IdResume; App.PlaySfx(Sfx.Click, 0.6f);
            }

            float H = r.ViewH;
            if (_state == State.Ready)
            {
                float pulse = 0.6f + 0.4f * Mathx.Sin(App.Time * 5);
                string msg = App.Platform.IsTouch ? "TAP TO START" : "PRESS SPACE OR CLICK TO START";
                r.Sprite(Tx.GlowLine, W / 2, H - 130, 900, 120, 0, new Col(0, 0, 0, 0.5f), Blend.Alpha);
                App.Ui.Glow(msg, W / 2, H - 130, 44, T.Edge.WithA(pulse), Align.Center, FontId.Ui, 0.6f);
            }
            if (!string.IsNullOrEmpty(_info.Hint) && (_state == State.Ready || _runTime < 6f))
            {
                float a = _state == State.Ready ? 1f : Mathx.Clamp01((6f - _runTime) / 1.5f);
                r.Sprite(Tx.GlowLine, W / 2, H - 62, 1100, 90, 0, new Col(0, 0, 0, 0.45f * a), Blend.Alpha);
                r.Text(_info.Hint, W / 2, H - 62, 32, Col.White.WithA(0.9f * a), Align.Center, FontId.Ui);
            }
        }

        string LevelLabel()
        {
            int idx = 1;
            foreach (var l in App.LevelsInWorld(_info.World)) { if (l.Number == _number) break; idx++; }
            return _info.World + "-" + idx;
        }

        void Dim(float a) { App.R.Sprite(Tx.Pixel, App.R.ViewW / 2, App.R.ViewH / 2, App.R.ViewW, App.R.ViewH, 0, new Col(0.01f, 0.01f, 0.04f, a), Blend.Alpha); }

        void DrawPause(bool interactive)
        {
            var r = App.R; var ui = App.Ui;
            Dim(0.62f);
            float W = r.ViewW, H = r.ViewH;
            var panel = Rect.Centered(W / 2, H / 2, 620, 640);
            ui.Panel(panel, T.Edge);
            ui.Glow("PAUSED", W / 2, panel.Y + 80, 64, T.Edge, Align.Center, FontId.Title);
            if (!interactive) return;
            float y = panel.Y + 160, bw = 480, bh = 76, gap = 90, bx = W / 2 - bw / 2;
            if (ui.Button(IdResume, new Rect(bx, y, bw, bh), "RESUME", Tx.IconPlay, true)) Resume();
            if (ui.Button(IdRestart, new Rect(bx, y + gap, bw, bh), "RESTART", Tx.IconRetry)) { App.PlaySfx(Sfx.Click); ResetRun(true); }
            if (ui.Button(IdSettings, new Rect(bx, y + gap * 2, bw, bh), "SETTINGS", Tx.IconGear)) { App.PlaySfx(Sfx.Click); App.Push(new SettingsScreen()); }
            if (ui.Button(IdLevels, new Rect(bx, y + gap * 3, bw, bh), "LEVEL SELECT", Tx.IconGrid)) { App.PlaySfx(Sfx.Click); App.GoTo(new LevelSelectScreen(_info.World)); }
            if (ui.Button(IdMenu, new Rect(bx, y + gap * 4, bw, bh), "MAIN MENU", Tx.IconHome)) { App.PlaySfx(Sfx.Click); App.GoTo(new TitleScreen()); }
        }

        void DrawComplete(bool interactive)
        {
            var r = App.R; var ui = App.Ui;
            float t = _stateT;
            Dim(Mathx.Clamp01(t * 2) * 0.6f);
            if (t < 0.35f) return;
            float W = r.ViewW, H = r.ViewH;
            float k = Mathx.EaseOutBack(Mathx.Clamp01((t - 0.35f) * 3f));
            var panel = Rect.Centered(W / 2, H / 2 + (1 - k) * 80, 820, 760);
            ui.Panel(panel, T.Accent);
            ui.Glow("LEVEL COMPLETE", W / 2, panel.Y + 78, 60, T.Accent, Align.Center, FontId.Title);
            r.Text(_info.Name.ToUpperInvariant(), W / 2, panel.Y + 136, 30, Col.White.WithA(0.7f), Align.Center, FontId.Ui);

            // stars
            for (int i = 0; i < 3; i++)
            {
                float cx = W / 2 + (i - 1) * 150, cy = panel.Y + 250 - (i == 1 ? 18 : 0);
                bool popped = i < _starsPopped, got = (_runStars & (1 << i)) != 0;
                float pk = popped ? Mathx.EaseOutElastic(Mathx.Clamp01((t - 0.6f - i * 0.28f) * 2.2f)) : 0;
                r.Sprite(Tx.StarOutline, cx, cy, 120, 120, 0, Col.White.WithA(0.25f), Blend.Alpha);
                if (got && popped)
                {
                    r.Sprite(Tx.Glow, cx, cy, 260 * pk, 260 * pk, 0, WorldView.StarCol.WithA(0.55f), Blend.Add);
                    r.Sprite(Tx.Star, cx, cy, 120 * pk, 120 * pk, 0, WorldView.StarCol, Blend.Alpha);
                }
            }

            float y = panel.Y + 370;
            Stat("ORBS", _orbs + " / " + L.Orbs.Count, y, panel);
            Stat("TIME", Clock(_runTime) + (_newTime ? "   NEW RECORD" : ""), y + 48, panel);
            Stat("DEATHS", _deaths.ToString(), y + 96, panel);
            r.Text("SCORE", panel.X + 90, y + 160, 34, T.Edge, Align.Left, FontId.Ui);
            ui.Glow(((int)_shownScore).ToString("N0", System.Globalization.CultureInfo.InvariantCulture), panel.Right - 90, y + 160, 54, Col.White, Align.Right, FontId.Title, 0.5f);
            if (_newBest && t > 1.6f) r.Text("NEW BEST!", panel.Right - 90, y + 206, 26, WorldView.StarCol, Align.Right, FontId.Ui);
            if (_unlock.Length > 0 && t > 1.6f)
            {
                float u = Mathx.Clamp01((t - 1.6f) * 3);
                r.Sprite(Tx.GlowLine, W / 2, panel.Bottom + 50, 760, 90, 0, T.Accent.WithA(0.35f * u), Blend.Add);
                r.Text("NEW CHARACTER UNLOCKED:  " + _unlock, W / 2, panel.Bottom + 50, 34, Col.White.WithA(u), Align.Center, FontId.Ui);
            }

            if (!interactive || t < 0.8f) return;
            float bw = 220, bh = 78, by = panel.Bottom - 112, gap = 24;
            float bx = W / 2 - (bw * 3 + gap * 2) / 2;
            bool hasNext = App.Level(_number + 1) != null;
            if (ui.Button(IdCLevels, new Rect(bx, by, bw, bh), "LEVELS", Tx.IconGrid)) { App.PlaySfx(Sfx.Click); App.GoTo(new LevelSelectScreen(_info.World)); }
            if (ui.Button(IdRetry, new Rect(bx + bw + gap, by, bw, bh), "RETRY", Tx.IconRetry)) { App.PlaySfx(Sfx.Click); App.GoTo(new GameScreen(_number)); }
            if (ui.Button(IdNext, new Rect(bx + (bw + gap) * 2, by, bw, bh), hasNext ? "NEXT" : "FINISH", Tx.IconPlay, true)) { App.PlaySfx(Sfx.Click); NextLevel(); }
        }

        void Stat(string label, string value, float y, Rect panel)
        {
            App.R.Text(label, panel.X + 90, y, 30, Col.White.WithA(0.6f), Align.Left, FontId.Ui);
            App.R.Text(value, panel.Right - 90, y, 32, Col.White, Align.Right, FontId.Ui);
            App.R.Sprite(Tx.Pixel, panel.CX, y + 24, panel.W - 180, 1.5f, 0, Col.White.WithA(0.08f), Blend.Alpha);
        }
    }
}
