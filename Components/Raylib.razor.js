class Raylib {

    async preloadFile(path) {
        try {
            const response = await fetch(path);
            if (!response.ok) {
                console.error(`Failed to fetch: ${path} (${response.status})`);
                return;
            }
            const data = new Uint8Array(await response.arrayBuffer());

            const parts = path.split('/');
            let dir = '';
            for (let i = 0; i < parts.length - 1; i++) {
                dir += (i > 0 ? '/' : '') + parts[i];
                try {
                    Blazor.runtime.Module.FS.mkdir(dir);
                } catch (e) { /* directory may already exist */ }
            }

            Blazor.runtime.Module.FS.writeFile(path, data);
            console.log(`Preloaded: ${path}`);
        } catch (e) {
            console.error(`Error preloading ${path}:`, e);
        }
    }

    setMovementCaptureArmed(armed) {
        Blazor.runtime.Module['movementCaptureArmed'] = !!armed;
    }

    requestPointerLock() {
        const canvas = Blazor.runtime.Module['canvas'];
        if (canvas && !document.pointerLockElement) {
            canvas.requestPointerLock();
        }
    }

    isPointerLockActive() {
        return !!document.pointerLockElement;
    }

    consumePointerLockEvent() {
        const evt = this._pointerLockEvent ?? '';
        this._pointerLockEvent = null;
        return evt;
    }

    init(dotnetObject, id) {
        const canvas = document.getElementById(id)
        if (canvas) {
            Blazor.runtime.Module['canvas'] = canvas;
            canvas.tabIndex = 0;
            canvas.addEventListener('contextmenu', (e) => e.preventDefault());
            canvas.addEventListener('mousedown', () => canvas.focus());

            const movementKeys = ['KeyW', 'KeyA', 'KeyS', 'KeyD'];
            const tryMovementPointerLock = () => {
                if (Blazor.runtime.Module['movementCaptureArmed']
                    && !document.pointerLockElement) {
                    canvas.requestPointerLock();
                }
            };

            document.addEventListener('keydown', (e) => {
                if (movementKeys.includes(e.code))
                    tryMovementPointerLock();
            });

            canvas.addEventListener('keydown', (e) => {
                if (e.key === 'Tab')
                    e.preventDefault();
            });

            document.addEventListener('pointerlockchange', () => {
                this._pointerLockEvent = document.pointerLockElement ? 'acquired' : 'lost';
            });

            document.addEventListener('pointerlockerror', () => {
                this._pointerLockEvent = 'failed';
            });

            if (dotnetObject) {
                Blazor.runtime.Module['canvasInstance'] = dotnetObject;
                window.addEventListener("resize", this.resize, true);
            }
        }
    }

    async resolveRuntime() {
        if (this._runtimeApi)
            return this._runtimeApi;

        const blazorRuntime = globalThis.Blazor?.runtime;
        if (blazorRuntime) {
            const runtime = typeof blazorRuntime.then === 'function'
                ? await blazorRuntime
                : blazorRuntime;
            if (runtime?.getAssemblyExports) {
                this._runtimeApi = runtime;
                return runtime;
            }
        }

        const dotnetRuntime = globalThis.getDotnetRuntime?.(0);
        if (!dotnetRuntime)
            throw new Error('Dotnet runtime is not available');

        this._runtimeApi = typeof dotnetRuntime.then === 'function'
            ? await dotnetRuntime
            : dotnetRuntime;
        return this._runtimeApi;
    }

    async getExports() {
        if (this._exports)
            return this._exports;

        const runtime = await this.resolveRuntime();
        this._exports = await runtime.getAssemblyExports("Wolfrender.Blazor.Raylib.dll");
        return this._exports;
    }

    async resize(e) {
        const canvas = Blazor.runtime.Module['canvas']
        const dotnetObject = Blazor.runtime.Module['canvasInstance'];

        const dpr = Math.round(window.devicePixelRatio);
        const width = canvas.widthNative = canvas.width = canvas.clientWidth;
        const height = canvas.heightNative = canvas.height = canvas.clientHeight;

        const exports = await this.getExports();
        exports.Wolfrender.Blazor.Raylib.Components.Raylib.ResizeCanvas(dotnetObject, width, height, dpr);
    }

    syncCanvasSize() {
        void this.resize({});
    }

    setFramePacing(vsyncEnabled, targetFps) {
        const fps = Math.max(30, Math.min(240, targetFps | 0));
        Blazor.runtime.Module['framePacing'] = {
            vsync: !!vsyncEnabled,
            fps
        };
    }

    render(dotnetObject, id, fps) {
        if (dotnetObject) {
            if (!Blazor.runtime.Module['framePacing']) {
                this.setFramePacing(true, fps || 120);
            }

            let lastTime = performance.now();
            const localRender = async () => {
                try {
                    const exports = await this.getExports();

                    const now = performance.now();
                    let delta = now - lastTime;

                    const pacing = Blazor.runtime.Module['framePacing'];
                    const useVsync = pacing.vsync;
                    const frameCap = useVsync ? 0 : (1000.0 / pacing.fps);

                    if (useVsync || delta >= frameCap) {
                        exports.Wolfrender.Blazor.Raylib.Components.Raylib.EventAnimationFrame(dotnetObject, delta);
                        lastTime = now;
                    }
                } catch (e) {
                    console.error('Render loop failed to resolve dotnet exports:', e);
                }

                requestAnimationFrame(localRender);
            };
            localRender();
        }
        else {
            console.log("DotNetReference: ", dotnetObject);
        }
    }
}

export const raylib = new Raylib();
