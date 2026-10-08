package dev.ttro;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.fml.common.event.*;
import net.minecraftforge.fml.common.eventhandler.SubscribeEvent;
import net.minecraftforge.fml.common.gameevent.*;
import net.minecraftforge.common.MinecraftForge;
import net.minecraftforge.fml.client.registry.ClientRegistry;
import net.minecraftforge.fml.common.FMLCommonHandler;
import net.minecraft.client.Minecraft;
import net.minecraft.client.settings.KeyBinding;
import net.minecraftforge.client.event.MouseEvent;
import org.lwjgl.input.Keyboard;
import org.lwjgl.opengl.Display;
import yalter.mousetweaks.Main;
import yalter.mousetweaks.Constants;

@Mod(modid="ttro189",name="@BRAND@",version="@VERSION@",clientSideOnly=true,acceptedMinecraftVersions="[1.8.9]",guiFactory="dev.ttro.TtroGuiFactory")
public final class Core {
 public static Core instance; public static String message=""; public static boolean headFxEnabled,scoreboardEnabled;
 public static long combatUntil; public static int ticks; public static KeyBinding settingsKey,zoomKey,sprintKey;
 private static final Hud HUD=new Hud(); private static final Zoom ZOOM=new Zoom(); private static final Sprint SPRINT=new Sprint(); private static final Identity IDENTITY=new Identity();
 static Hud hud(){return HUD;}
 public static boolean available(String id){String required=Config.requirements.get(id);return required==null||net.minecraftforge.fml.common.Loader.isModLoaded(required);}
 static boolean canToggle(String id){return !Config.foundations.contains(id)&&(Config.module(id).get("enabled").getAsBoolean()||(Safety.allowed(id)&&available(id)));}
 private static final Crosshair CROSSHAIR=new Crosshair();
 private static boolean crosshairRegistered,hudRegistered,zoomRegistered,sprintRegistered,identityRegistered,inventoryRegistered;
 private static final Inventory INVENTORY=new Inventory();
 @Mod.EventHandler public void init(FMLInitializationEvent e)throws Exception{
  instance=this;Config.init();Display.setTitle(Config.brand+" | Minecraft 1.8.9");
  settingsKey=new KeyBinding(Config.brand+" Settings",Keyboard.KEY_RCONTROL,Config.brand);zoomKey=new KeyBinding(Config.brand+" Zoom",Keyboard.KEY_C,Config.brand);sprintKey=new KeyBinding(Config.brand+" Toggle Sprint",Keyboard.KEY_R,Config.brand);
  ClientRegistry.registerKeyBinding(settingsKey);ClientRegistry.registerKeyBinding(zoomKey);ClientRegistry.registerKeyBinding(sprintKey);
  MinecraftForge.EVENT_BUS.register(this);FMLCommonHandler.instance().bus().register(this);
  Main.initialize(Constants.EntryPoint.FORGE);apply();
  if(Boolean.getBoolean("ttro.captureFrames"))FMLCommonHandler.instance().bus().register(new Frames());
 }
 @Mod.EventHandler public void post(FMLPostInitializationEvent e){apply();}
 public static void apply(){if(instance==null||Config.data==null)return;
  headFxEnabled=Config.enabled("headfx")&&!Config.text("headfx","preset").equals("OFF");scoreboardEnabled=Config.enabled("scoreboard");
  boolean next=Hud.anyEnabled();if(next!=hudRegistered){hudRegistered=next;if(next){MinecraftForge.EVENT_BUS.register(HUD);FMLCommonHandler.instance().bus().register(HUD);}else{MinecraftForge.EVENT_BUS.unregister(HUD);FMLCommonHandler.instance().bus().unregister(HUD);}}
  next=Config.enabled("crosshair");if(next!=crosshairRegistered){crosshairRegistered=next;if(next)MinecraftForge.EVENT_BUS.register(CROSSHAIR);else MinecraftForge.EVENT_BUS.unregister(CROSSHAIR);}
  next=Config.enabled("zoom");if(next!=zoomRegistered){zoomRegistered=next;if(next)MinecraftForge.EVENT_BUS.register(ZOOM);else MinecraftForge.EVENT_BUS.unregister(ZOOM);}
  next=Config.enabled("togglesprint");if(next!=sprintRegistered){sprintRegistered=next;if(next)FMLCommonHandler.instance().bus().register(SPRINT);else{FMLCommonHandler.instance().bus().unregister(SPRINT);SPRINT.release();}}
  next=Config.enabled("identity");if(next!=identityRegistered){identityRegistered=next;if(next)MinecraftForge.EVENT_BUS.register(IDENTITY);else MinecraftForge.EVENT_BUS.unregister(IDENTITY);}
  next=Config.enabled("inventory");if(next!=inventoryRegistered){inventoryRegistered=next;if(next)FMLCommonHandler.instance().bus().register(INVENTORY);else FMLCommonHandler.instance().bus().unregister(INVENTORY);}
  if(Main.config!=null){boolean inv=Config.enabled("inventory");Main.config.rmbTweak=inv&&Config.bool("inventory","rightDrag");Main.config.lmbTweakWithItem=inv&&Config.bool("inventory","leftCollect");Main.config.lmbTweakWithoutItem=inv&&Config.bool("inventory","shiftDrag");Main.config.wheelTweak=inv&&Config.bool("inventory","wheel");Main.ttroShiftDrag=inv&&Config.bool("inventory","shiftDrag");Main.ttroReverse=Config.text("inventory","scrollDirection").equals("Reverse");Main.config.wheelSearchOrder=yalter.mousetweaks.WheelSearchOrder.fromId(Config.text("inventory","searchDirection").equals("First to last")?0:1);}
  PatcherBridge.apply();HUD.refresh();HypixelBridge.init();
 }
 @SubscribeEvent public void tick(TickEvent.ClientTickEvent e){if(e.phase!=TickEvent.Phase.END)return;ticks++;
  if(settingsKey.isPressed())Minecraft.getMinecraft().displayGuiScreen(new SettingsScreen(Minecraft.getMinecraft().currentScreen));
  if(ticks%20==0){Safety.update();if(Config.changed()){Config.load();apply();}}
 }
 public static class Inventory { @SubscribeEvent public void render(TickEvent.RenderTickEvent e){if(e.phase==TickEvent.Phase.START&&Minecraft.getMinecraft().currentScreen!=null)Main.onUpdateInGame();}}
 @SubscribeEvent public void disconnect(net.minecraftforge.fml.common.network.FMLNetworkEvent.ClientDisconnectionFromServerEvent e){Minecraft.getMinecraft().addScheduledTask(new Runnable(){public void run(){ModernTab.reset();HypixelBridge.officialHello=false;HypixelBridge.location="Unknown";Safety.hypixel=false;combatUntil=0;HUD.reset();apply();}});}
 @SubscribeEvent public void attack(net.minecraftforge.event.entity.player.AttackEntityEvent e){if(e.entityPlayer==Minecraft.getMinecraft().thePlayer)combatUntil=System.currentTimeMillis()+4000;}
 public static class Zoom { @SubscribeEvent public void fov(net.minecraftforge.client.event.FOVUpdateEvent e){if(zoomKey.isKeyDown()&&Minecraft.getMinecraft().currentScreen==null)e.newfov/=Config.number("zoom","factor");}}
 public static class Sprint {boolean active;
  @SubscribeEvent public void tick(TickEvent.ClientTickEvent e){if(e.phase!=TickEvent.Phase.END)return;Minecraft m=Minecraft.getMinecraft();if(sprintKey.isPressed())active=!active;int k=m.gameSettings.keyBindSprint.getKeyCode();if(m.currentScreen==null&&m.thePlayer!=null)KeyBinding.setKeyBindState(k,active||(k>=0?Keyboard.isKeyDown(k):org.lwjgl.input.Mouse.isButtonDown(k+100)));}
  void release(){active=false;Minecraft m=Minecraft.getMinecraft();if(m.gameSettings!=null)KeyBinding.setKeyBindState(m.gameSettings.keyBindSprint.getKeyCode(),false);}
 }
}
