package dev.ttro;
import java.lang.reflect.*;
import java.util.*;
/** Integrates an independent upstream Mod. No Patcher code is copied or forked. */
public final class PatcherBridge {
 private static Class<?> config;private static final Map<String,Field> fields=new HashMap<String,Field>();
 static Class<?> config(){if(config==null&&net.minecraftforge.fml.common.Loader.isModLoaded("patcher")){try{config=Class.forName("club.sk1er.patcher.config.PatcherConfig");}catch(ClassNotFoundException ignored){}}return config;}
 static void set(String name,Object value){try{Field f=fields.get(name);if(f==null){f=config().getField(name);fields.put(name,f);}f.set(null,value);}catch(Exception e){Core.message="PolyPatcher integration unavailable: "+name;}}
 /** Ttro owns only these settings. OFF explicitly restores vanilla behavior,
  * including when Patcher ships with Fullbright enabled by default. */
 public static void apply(){if(Config.data==null||config()==null)return;
  set("fullbright",Config.enabled("fullbright"));
  boolean fire=Config.enabled("reducedfire");
  set("fireOverlayHeight",fire?Config.number("reducedfire","height"):0f);
  set("fireOverlayOpacityI",fire?(int)Config.number("reducedfire","opacity"):100);
  set("fireOverlayOpacity",fire?Config.number("reducedfire","opacity")/100f:1f);
  boolean fov=Config.enabled("fov");set("allowFovModifying",fov);
  for(String name:new String[]{"sprintingFovModifierFloat","flyingFovModifierFloat","bowFovModifierFloat","speedFovModifierFloat","slownessFovModifierFloat"})set(name,fov?0f:1f);
  set("removeWaterFov",Config.enabled("waterfov"));
  set("cleanerNightVision",Config.enabled("nightvision"));
  set("preventOverflowHotbarScrolling",Config.enabled("scrollfix"));
  set("disableEnchantmentGlint",Config.enabled("glint"));
  set("smoothScrolling",Config.enabled("smoothscroll"));
  set("numericalEnchants",Config.enabled("numericalenchants"));
  set("timestamps",Config.enabled("chatstamp"));
  set("smartDisconnect",Config.enabled("smartdisconnect"));
  set("confirmQuit",Config.enabled("confirmquit"));
  set("layersInTab",Config.enabled("tablayers"));
  set("downscalePackImages",Config.enabled("packimages"));
  set("cacheFontData",Config.enabled("fontcache"));
  set("batchModelRendering",Config.enabled("batchmodels"));
  set("cacheEntrypoints",Config.enabled("entrycache"));
  set("windowedFullscreen",Config.enabled("windowedfullscreen"));
  // Preserve the potion's useful visual cue; cleanup only removes flashing.
  if(Config.enabled("nightvision"))set("disableNightVision",false);
  set("newKeybindHandling",Config.enabled("inputfix"));
  String layout=Config.text("inputfix","layout");int index=Arrays.asList("QWERTY","BE AZERTY","FR AZERTY","DE QWERTZ").indexOf(layout);
  set("keyboardLayout",Config.enabled("inputfix")?Math.max(0,index):0);
 }
 private PatcherBridge(){}
}
