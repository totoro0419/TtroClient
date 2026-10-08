package dev.ttro;
import net.minecraft.launchwrapper.IClassTransformer;
import org.objectweb.asm.*;
import org.objectweb.asm.tree.*;
/** Three bounded rendering hooks. Gameplay/network/input behavior is untouched. */
public final class Transformer implements IClassTransformer {
 public byte[] transform(String name,String transformed,byte[] bytes){
  if(bytes==null)return null;boolean item=transformed.equals("net.minecraft.client.renderer.entity.RenderItem"),hud=transformed.equals("net.minecraft.client.gui.GuiIngame");boolean tab=transformed.equals("net.minecraft.client.gui.GuiPlayerTabOverlay");if(!item&&!hud&&!tab)return bytes;
  ClassNode c=new ClassNode();new ClassReader(bytes).accept(c,0);boolean changed=false;
  for(Object method:c.methods){MethodNode m=(MethodNode)method;
   if(item&&(m.name.equals("renderItemAndEffectIntoGUI")||m.name.equals("func_180450_b"))&&m.desc.equals("(Lnet/minecraft/item/ItemStack;II)V")){
    InsnList pre=new InsnList();pre.add(new VarInsnNode(Opcodes.ALOAD,1));pre.add(new VarInsnNode(Opcodes.ILOAD,2));pre.add(new VarInsnNode(Opcodes.ILOAD,3));pre.add(new MethodInsnNode(Opcodes.INVOKESTATIC,"dev/ttro/HeadFx","begin","(Lnet/minecraft/item/ItemStack;II)V",false));m.instructions.insert(pre);
    for(AbstractInsnNode n=m.instructions.getFirst();n!=null;n=n.getNext())if(n.getOpcode()==Opcodes.RETURN){InsnList end=new InsnList();end.add(new VarInsnNode(Opcodes.ALOAD,1));end.add(new VarInsnNode(Opcodes.ILOAD,2));end.add(new VarInsnNode(Opcodes.ILOAD,3));end.add(new MethodInsnNode(Opcodes.INVOKESTATIC,"dev/ttro/HeadFx","end","(Lnet/minecraft/item/ItemStack;II)V",false));m.instructions.insertBefore(n,end);}changed=true;
   }
   if(hud&&(m.name.equals("renderScoreboard")||m.name.equals("func_180475_a"))&&m.desc.equals("(Lnet/minecraft/scoreboard/ScoreObjective;Lnet/minecraft/client/gui/ScaledResolution;)V")){
    InsnList p=new InsnList();LabelNode next=new LabelNode();p.add(new VarInsnNode(Opcodes.ALOAD,1));p.add(new VarInsnNode(Opcodes.ALOAD,2));p.add(new MethodInsnNode(Opcodes.INVOKESTATIC,"dev/ttro/Scoreboard","render",m.desc.substring(0,m.desc.length()-1)+"Z",false));p.add(new JumpInsnNode(Opcodes.IFEQ,next));p.add(new InsnNode(Opcodes.RETURN));p.add(next);p.add(new FrameNode(Opcodes.F_SAME,0,null,0,null));m.instructions.insert(p);changed=true;
   }
   if(tab&&(m.name.equals("renderPlayerlist")||m.name.equals("func_175249_a"))&&m.desc.equals("(ILnet/minecraft/scoreboard/Scoreboard;Lnet/minecraft/scoreboard/ScoreObjective;)V")){
    InsnList p=new InsnList();LabelNode next=new LabelNode();p.add(new VarInsnNode(Opcodes.ALOAD,0));p.add(new VarInsnNode(Opcodes.ILOAD,1));p.add(new VarInsnNode(Opcodes.ALOAD,2));p.add(new VarInsnNode(Opcodes.ALOAD,3));p.add(new MethodInsnNode(Opcodes.INVOKESTATIC,"dev/ttro/ModernTab","render","(Lnet/minecraft/client/gui/GuiPlayerTabOverlay;ILnet/minecraft/scoreboard/Scoreboard;Lnet/minecraft/scoreboard/ScoreObjective;)Z",false));p.add(new JumpInsnNode(Opcodes.IFEQ,next));p.add(new InsnNode(Opcodes.RETURN));p.add(next);p.add(new FrameNode(Opcodes.F_SAME,0,null,0,null));m.instructions.insert(p);changed=true;
   }
  }
  if(!changed){System.err.println("Ttro Client: rendering hook unavailable for "+transformed);return bytes;}ClassWriter w=new ClassWriter(ClassWriter.COMPUTE_MAXS);c.accept(w);return w.toByteArray();
 }
}
