class Raylib {
    
    async preloadFile(path) {
        try {
            const response = await fetch(path);
            if (!response.ok) {
                console.error(`Failed to fetch: ${path} (${response.status})`);
                return;
            }
            const data = new Uint8Array(await response.arrayBuffer());
            
            // Ensure parent directories exist in the Emscripten VFS
            const parts = path.split('/');
            let dir = '';
            for (let i = 0; i < parts.length - 1; i++) {
                dir += (i > 0 ? '/' : '') + parts[i];
                try {
                    Blazor.runtime.Module.FS.mkdir(dir);
                } catch(e) { /* directory may already exist */ }
            }
            
            Blazor.runtime.Module.FS.writeFile(path, data);
            console.log(`Preloaded: ${path}`);
        } catch(e) {
            console.error(`Error preloading ${path}:`, e);
        }
    }

    init(dotnetObject, id) {
        const canvas = document.getElementById(id)
        if (canvas) {
            Blazor.runtime.Module['canvas'] = canvas;
            canvas.addEventListener('contextmenu',(e) => e.preventDefault());
            
            if (dotnetObject) {
                Blazor.runtime.Module['canvasInstance'] = dotnetObject;
                window.addEventListener("resize", this.resize, true);
                this.resize({});
            }
        }
    }
    
    async resize(e) {
        const canvas = Blazor.runtime.Module['canvas']
        const dotnetObject =  Blazor.runtime.Module['canvasInstance'];
        
        const dpr = Math.round(window.devicePixelRatio);
        const width =  canvas.widthNative = canvas.width =  canvas.clientWidth;
        const height = canvas.heightNative = canvas.height =  canvas.clientHeight;


        const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
        var exports = await getAssemblyExports("Wolfrender.Blazor.Raylib.dll");
        exports.Wolfrender.Blazor.Raylib.Components.Raylib.ResizeCanvas(dotnetObject, width, height, dpr);
    }
    
    render(dotnetObject, id, fps) {
        if (dotnetObject) {
            const frameCap = 1000.0 / (fps + 16.0);
            let lastTime = performance.now();
            const localRender = async (time) => {
                const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
                var exports = await getAssemblyExports("Wolfrender.Blazor.Raylib.dll");
                
                const now = performance.now();
                let delta = now - lastTime;
                
               if (delta > frameCap) {
                   exports.Wolfrender.Blazor.Raylib.Components.Raylib.EventAnimationFrame(dotnetObject, delta);
                   lastTime = now;
               }
                
               requestAnimationFrame(localRender);
            };
            localRender(0);
        }
        else {
            console.log("DotNetReference: ", dotnetObject);
        }
    }
}

export const raylib = new Raylib();
