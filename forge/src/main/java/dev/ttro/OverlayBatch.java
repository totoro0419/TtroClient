package dev.ttro;
import net.minecraft.client.renderer.GlStateManager;
import net.minecraft.client.renderer.Tessellator;
import net.minecraft.client.renderer.WorldRenderer;
import net.minecraft.client.renderer.vertex.DefaultVertexFormats;
/** One draw for a small overlay's rectangles; uses Minecraft's existing buffer. */
final class OverlayBatch {
 static void begin(){GlStateManager.enableBlend();GlStateManager.disableTexture2D();GlStateManager.tryBlendFuncSeparate(770,771,1,0);Tessellator.getInstance().getWorldRenderer().begin(7,DefaultVertexFormats.POSITION_COLOR);}
 static void rect(int x1,int y1,int x2,int y2,int argb){WorldRenderer w=Tessellator.getInstance().getWorldRenderer();int a=argb>>>24,r=argb>>16&255,g=argb>>8&255,b=argb&255;w.pos(x1,y2,0).color(r,g,b,a).endVertex();w.pos(x2,y2,0).color(r,g,b,a).endVertex();w.pos(x2,y1,0).color(r,g,b,a).endVertex();w.pos(x1,y1,0).color(r,g,b,a).endVertex();}
 static void end(){Tessellator.getInstance().draw();GlStateManager.enableTexture2D();GlStateManager.disableBlend();GlStateManager.color(1,1,1,1);}
 private OverlayBatch(){}
}
