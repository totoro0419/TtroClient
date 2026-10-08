package dev.ttro;
import java.util.Map;
import net.minecraftforge.fml.relauncher.IFMLLoadingPlugin;
@IFMLLoadingPlugin.SortingIndex(1001)
@IFMLLoadingPlugin.MCVersion("1.8.9")
@IFMLLoadingPlugin.TransformerExclusions({"dev.ttro.Transformer","dev.ttro.LoadingPlugin"})
public final class LoadingPlugin implements IFMLLoadingPlugin {
 public String[] getASMTransformerClass(){return new String[]{"dev.ttro.Transformer"};}
 public String getModContainerClass(){return null;}public String getSetupClass(){return null;}public void injectData(Map<String,Object> data){}public String getAccessTransformerClass(){return null;}
}
