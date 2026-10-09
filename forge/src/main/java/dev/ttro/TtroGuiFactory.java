package dev.ttro;
import java.util.Set;
import java.util.Collections;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiScreen;
import net.minecraftforge.fml.client.IModGuiFactory;
/** Keeps settings reachable from Forge even when the user changes a shortcut. */
public final class TtroGuiFactory implements IModGuiFactory {
 public void initialize(Minecraft minecraft){}
 public Class<? extends GuiScreen> mainConfigGuiClass(){return SettingsScreen.class;}
 public Set<RuntimeOptionCategoryElement> runtimeGuiCategories(){return Collections.emptySet();}
 public RuntimeOptionGuiHandler getHandlerFor(RuntimeOptionCategoryElement element){return null;}
}
