package dev.ttro;
import com.google.gson.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import net.minecraft.client.Minecraft;

public final class Config {
 public static final Gson GSON=new GsonBuilder().setPrettyPrinting().create();
 public static JsonObject data; public static JsonArray catalog; public static File file; private static long modified=-1;
 public static String brand="Ttro Client";
 public static final Map<String,String> requirements=new HashMap<String,String>();
 public static final Set<String> foundations=new HashSet<String>();
 public static void init() throws IOException {
  catalog=readResource("modules.json").getAsJsonArray();
  for(JsonElement item:catalog){JsonObject m=item.getAsJsonObject();String id=m.get("id").getAsString();if(m.has("requires"))requirements.put(id,m.get("requires").getAsString());if(m.has("control")&&m.get("control").getAsString().equals("foundation"))foundations.add(id);}
  brand=readResource("product.json").getAsJsonObject().get("name").getAsString();
  file=new File(Minecraft.getMinecraft().mcDataDir,"config/ttro-client.json");
  data=defaults();load();
 }
 static JsonElement readResource(String n) throws IOException {
  try(InputStream in=Config.class.getClassLoader().getResourceAsStream(n)){
   if(in==null)throw new IOException("Missing "+n);
   return new JsonParser().parse(new InputStreamReader(in,StandardCharsets.UTF_8));
  }
 }
 public static JsonObject defaults(){
  JsonObject root=new JsonObject(),modules=new JsonObject(),hud=new JsonObject();root.addProperty("schema",2);
  for(JsonElement e:catalog){JsonObject m=e.getAsJsonObject(),o=new JsonObject();o.addProperty("enabled",!m.get("status").getAsString().equals("candidate")&&m.get("default").getAsBoolean());
   for(Map.Entry<String,JsonElement> prop:m.getAsJsonObject("settings").entrySet())o.add(prop.getKey(),prop.getValue().getAsJsonObject().get("default"));
   modules.add(m.get("id").getAsString(),o);
  }
  hud.addProperty("layout","clusters");hud.addProperty("x",12);hud.addProperty("y",12);hud.addProperty("scale",1);hud.add("elements",defaultElements());root.add("modules",modules);root.add("hud",hud);return root;
 }
 static JsonObject defaultElements(){JsonObject elements=new JsonObject();for(int i=0;i<Hud.ELEMENT_IDS.length;i++){JsonObject p=new JsonObject();p.addProperty("x",12+(i/6)*220);p.addProperty("y",12+(i%6)*26);p.addProperty("scale",1);elements.add(Hud.ELEMENT_IDS[i],p);}return elements;}
 public static void load(){
  if(!file.exists())return;
  try(Reader r=new InputStreamReader(new FileInputStream(file),StandardCharsets.UTF_8)){
   JsonObject read=new JsonParser().parse(r).getAsJsonObject();upgrade(read);validate(read);data=read;modified=file.lastModified();
  }catch(Exception e){Core.message="Settings could not be loaded: "+e.getClass().getSimpleName();modified=file.lastModified();}
 }
 public static boolean changed(){return file.exists()&&file.lastModified()!=modified;}
 public static JsonObject module(String id){return data.getAsJsonObject("modules").getAsJsonObject(id);}
 public static boolean enabled(String id){JsonObject m=module(id);return m!=null&&m.has("enabled")&&m.get("enabled").getAsBoolean()&&Safety.allowed(id)&&Core.available(id);}
 public static boolean bool(String id,String key){return module(id).get(key).getAsBoolean();}
 public static String text(String id,String key){return module(id).get(key).getAsString();}
 public static float number(String id,String key){return module(id).get(key).getAsFloat();}
 /** Additive schema-2 catalog migration preserves each existing user value. */
 static void upgrade(JsonObject read){if(!read.has("schema")||read.get("schema").getAsInt()!=2)return;JsonObject ms=read.getAsJsonObject("modules"),defaults=defaults().getAsJsonObject("modules");if(ms==null)return;
  for(Map.Entry<String,JsonElement> entry:defaults.entrySet()){if(!ms.has(entry.getKey())){ms.add(entry.getKey(),entry.getValue());continue;}JsonObject values=ms.getAsJsonObject(entry.getKey());for(Map.Entry<String,JsonElement> property:entry.getValue().getAsJsonObject().entrySet())if(!values.has(property.getKey()))values.add(property.getKey(),property.getValue());}
  for(String id:foundations)ms.getAsJsonObject(id).addProperty("enabled",true);
 }
 public static void validate(JsonObject r){
  if(!r.has("schema")||r.get("schema").getAsInt()!=2)throw new IllegalArgumentException("schema 2 required");
  JsonObject ms=r.getAsJsonObject("modules");
  for(JsonElement item:catalog){JsonObject m=item.getAsJsonObject();String id=m.get("id").getAsString();JsonObject o=ms.getAsJsonObject(id);if(o==null)throw new IllegalArgumentException("Missing module "+id);
   if(!o.get("enabled").getAsJsonPrimitive().isBoolean())throw new IllegalArgumentException("Boolean required");
   if(m.get("status").getAsString().equals("candidate")&&o.get("enabled").getAsBoolean())throw new IllegalArgumentException("Unimplemented module "+id);
   for(Map.Entry<String,JsonElement> p:m.getAsJsonObject("settings").entrySet()){
    JsonObject d=p.getValue().getAsJsonObject();JsonElement v=o.get(p.getKey());String t=d.get("type").getAsString();
    if(v==null)throw new IllegalArgumentException("Missing property");
    if(t.equals("boolean")&&!v.getAsJsonPrimitive().isBoolean())throw new IllegalArgumentException("Boolean required");
    if(t.equals("number")){if(!v.getAsJsonPrimitive().isNumber())throw new IllegalArgumentException("Number required");double n=v.getAsDouble();if(Double.isNaN(n)||Double.isInfinite(n)||n<d.get("min").getAsDouble()||n>d.get("max").getAsDouble())throw new IllegalArgumentException("Out of range");}
    if(t.equals("select")){boolean match=false;for(JsonElement x:d.getAsJsonArray("options"))if(x.getAsString().equals(v.getAsString()))match=true;if(!match)throw new IllegalArgumentException("Invalid choice");}
   }
  }
  JsonObject h=r.getAsJsonObject("hud");for(String key:new String[]{"x","y","scale"}){JsonElement value=h.get(key);if(value==null||!value.getAsJsonPrimitive().isNumber()||!Double.isFinite(value.getAsDouble()))throw new IllegalArgumentException("HUD number");}for(String key:new String[]{"x","y"})if(h.get(key).getAsDouble()<0||h.get(key).getAsDouble()>10000)throw new IllegalArgumentException("HUD position");String layout=h.get("layout").getAsString();if(!layout.equals("clusters")&&!layout.equals("individual"))throw new IllegalArgumentException("HUD layout");double scale=h.get("scale").getAsDouble();if(!Double.isFinite(scale)||scale<.5||scale>3)throw new IllegalArgumentException("HUD scale");
  if(!h.has("elements"))h.add("elements",defaultElements());JsonObject elements=h.getAsJsonObject("elements");
  for(Map.Entry<String,JsonElement> entry:elements.entrySet()){if(!Arrays.asList(Hud.ELEMENT_IDS).contains(entry.getKey()))throw new IllegalArgumentException("Unknown HUD");JsonObject p=entry.getValue().getAsJsonObject();for(String key:new String[]{"x","y","scale"}){JsonElement value=p.get(key);double minimum=key.equals("scale")?.5:0,maximum=key.equals("scale")?3:10000;if(value==null||!value.getAsJsonPrimitive().isNumber()||!Double.isFinite(value.getAsDouble())||value.getAsDouble()<minimum||value.getAsDouble()>maximum)throw new IllegalArgumentException("HUD position/scale");}}
  JsonObject fallback=defaultElements();for(String id:Hud.ELEMENT_IDS)if(!elements.has(id))elements.add(id,fallback.get(id));
 }
 public static boolean save(){
  try{validate(data);Path p=file.toPath();Files.createDirectories(p.getParent());Path tmp=Files.createTempFile(p.getParent(),"ttro-",".tmp");
   try(FileOutputStream stream=new FileOutputStream(tmp.toFile())){stream.write(GSON.toJson(data).getBytes(StandardCharsets.UTF_8));stream.getFD().sync();}
   try{Files.move(tmp,p,StandardCopyOption.ATOMIC_MOVE,StandardCopyOption.REPLACE_EXISTING);}catch(AtomicMoveNotSupportedException e){Files.move(tmp,p,StandardCopyOption.REPLACE_EXISTING);}modified=file.lastModified();Core.apply();return true;
  }catch(Exception e){Core.message="Save failed: "+e.getClass().getSimpleName();return false;}
 }
 private Config(){}
}
