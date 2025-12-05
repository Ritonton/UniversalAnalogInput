// Build script to embed Windows resources (icon) into tray executable.

fn main() {
    // Only compile resources on Windows.
    #[cfg(target_os = "windows")]
    {
        // Embed the icon using winres.
        let mut res = winres::WindowsResource::new();
        res.set_icon("..\\shared\\assets\\icon.ico");

        if let Err(e) = res.compile() {
            eprintln!("Warning: Failed to compile Windows resources: {}", e);
            eprintln!("The tray executable will not have an embedded icon.");
        }
    }

    // Rebuild if icon changes.
    println!("cargo:rerun-if-changed=../shared/assets/icon.ico");
}
