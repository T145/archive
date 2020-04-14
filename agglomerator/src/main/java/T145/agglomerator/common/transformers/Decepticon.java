package T145.agglomerator.common.transformers;

import java.io.File;
import java.util.Arrays;
import java.util.Comparator;
import java.util.Locale;
import java.util.Map;

import cpw.mods.fml.relauncher.IFMLLoadingPlugin;

@IFMLLoadingPlugin.MCVersion("1.6.4")
public class Decepticon implements IFMLLoadingPlugin {
	// needed for later Forge versions
	public String[] getLibraryRequestClass() {
		return null;
	}

	@Override
	public String[] getASMTransformerClass() {
		return new String[] { "T145.agglomerator.common.transformers.Autobot" };
	}

	@Override
	public String getModContainerClass() {
		return null;
	}

	@Override
	public String getSetupClass() {
		return null;
	}

	@Override
	public void injectData(final Map<String, Object> data) {}

	public static void sort(final Object[] a) {
		final File[] files = (File[]) a;
		Arrays.sort(files, new Comparator<File>() {
			@Override
			public int compare(final File o1, final File o2) {
				return o1.getAbsolutePath().toLowerCase(Locale.US).compareTo(o2.getAbsolutePath().toLowerCase(Locale.US));
			}
		});
	}
}
