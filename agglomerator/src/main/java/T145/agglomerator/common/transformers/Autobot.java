package T145.agglomerator.common.transformers;

import net.minecraft.enchantment.Enchantment;
import net.minecraft.launchwrapper.IClassTransformer;
import net.minecraftforge.common.Configuration;

import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassVisitor;
import org.objectweb.asm.ClassWriter;
import org.objectweb.asm.tree.AbstractInsnNode;
import org.objectweb.asm.tree.ClassNode;
import org.objectweb.asm.tree.InsnList;
import org.objectweb.asm.tree.InsnNode;
import org.objectweb.asm.tree.MethodInsnNode;
import org.objectweb.asm.tree.MethodNode;
import org.objectweb.asm.tree.VarInsnNode;

import T145.agglomerator.common.Agglomerator;
import cpw.mods.fml.common.asm.transformers.deobf.FMLDeobfuscatingRemapper;
import cpw.mods.fml.common.discovery.ModDiscoverer;
import cpw.mods.fml.relauncher.FMLRelaunchLog;

public class Autobot implements IClassTransformer {
	public byte[] sort(final String name, final String deobf, final byte[] data) {
		if (!name.startsWith(ModDiscoverer.class.getName()))
			return data;
		System.out.println(" ### Agglomerator ### ");
		final ClassNode classNode = new ClassNode();
		final ClassReader classReader = new ClassReader(data);
		classReader.accept((ClassVisitor) classNode, 0);
		FMLRelaunchLog.info("Agglomerator: Processing class ", new Object[0]);
		for (final MethodNode method : classNode.methods) {
			if (!method.name.equals("findModDirMods"))
				continue;
			FMLRelaunchLog.info(" Method Located: ", new Object[0]);
			for (int x = 0; x < method.instructions.size(); ++x) {
				final AbstractInsnNode isn = method.instructions.get(x);
				if (isn instanceof MethodInsnNode) {
					final MethodInsnNode call = (MethodInsnNode) isn;
					if (call.name.equals("sort") && call.desc.equals("([Ljava/lang/Object;)V") && call.owner.equals("java/util/Arrays")) {
						call.owner = Decepticon.class.getName();
						FMLRelaunchLog.info(call.name + " " + call.desc + " " + call.owner, new Object[0]);
					}
				}
			}
		}
		final ClassWriter writer = new ClassWriter(1);
		classNode.accept((ClassVisitor) writer);
		return writer.toByteArray();
	}

	public byte[] fix(final String name, final String decodedName, byte[] code) {
		if (decodedName.equals(Configuration.class.getName())) {
			code = inject(code, decodedName, "getBlockInternal", "(Ljava/lang/String;Ljava/lang/String;ILjava/lang/String;II)Lnet/minecraftforge/common/Property;", -1, new InjectHelper() {
				@Override
				void inject(final InsnList list) {
					list.add((AbstractInsnNode) new VarInsnNode(25, 0));
					list.add((AbstractInsnNode) new VarInsnNode(25, 1));
					list.add((AbstractInsnNode) new VarInsnNode(25, 2));
					list.add((AbstractInsnNode) new VarInsnNode(21, 3));
					list.add((AbstractInsnNode) new VarInsnNode(25, 4));
					list.add((AbstractInsnNode) new VarInsnNode(21, 5));
					list.add((AbstractInsnNode) new VarInsnNode(21, 6));
					list.add((AbstractInsnNode) new MethodInsnNode(184, Agglomerator.class.getName(), "configGetBlockInternal", "(Lnet/minecraftforge/common/Configuration;Ljava/lang/String;Ljava/lang/String;ILjava/lang/String;II)Lnet/minecraftforge/common/Property;"));
					list.add((AbstractInsnNode) new InsnNode(176));
				}
			});
			code = inject(code, decodedName, "getItem", "(Ljava/lang/String;Ljava/lang/String;ILjava/lang/String;)Lnet/minecraftforge/common/Property;", -1, new InjectHelper() {
				@Override
				void inject(final InsnList list) {
					list.add((AbstractInsnNode) new VarInsnNode(25, 0));
					list.add((AbstractInsnNode) new VarInsnNode(25, 1));
					list.add((AbstractInsnNode) new VarInsnNode(25, 2));
					list.add((AbstractInsnNode) new VarInsnNode(21, 3));
					list.add((AbstractInsnNode) new VarInsnNode(25, 4));
					list.add((AbstractInsnNode) new MethodInsnNode(184, Agglomerator.class.getName(), "configGetItem", "(Lnet/minecraftforge/common/Configuration;Ljava/lang/String;Ljava/lang/String;ILjava/lang/String;)Lnet/minecraftforge/common/Property;"));
					list.add((AbstractInsnNode) new InsnNode(176));
				}
			});
			code = inject(code, decodedName, "get", "(Ljava/lang/String;Ljava/lang/String;ILjava/lang/String;)Lnet/minecraftforge/common/Property;", -1, new InjectHelper() {
				@Override
				void inject(final InsnList list) {
					list.add((AbstractInsnNode) new VarInsnNode(25, 0));
					list.add((AbstractInsnNode) new VarInsnNode(25, 1));
					list.add((AbstractInsnNode) new VarInsnNode(25, 2));
					list.add((AbstractInsnNode) new VarInsnNode(21, 3));
					list.add((AbstractInsnNode) new MethodInsnNode(184, Agglomerator.class.getName(), "configGet", "(Lnet/minecraftforge/common/Configuration;Ljava/lang/String;Ljava/lang/String;I)I"));
					list.add((AbstractInsnNode) new VarInsnNode(54, 3));
				}
			});
		} else if (decodedName.equals(Enchantment.class.getName()))
			code = injectSimple(code, decodedName, "<init>", "(IIL" + (name.equals(decodedName) ? "net/minecraft/enchantment/EnumEnchantmentType" : FMLDeobfuscatingRemapper.INSTANCE.unmap("net/minecraft/enchantment/EnumEnchantmentType")) + ";)V", 183, Agglomerator.class.getName(), "transformEnchantmentId", "(I)I");
		return code;
	}

	@Override
	public byte[] transform(final String name, final String decodedName, byte[] code) {
		sort(name, decodedName, code); // first sort the mods, then fix the IDs
		return fix(name, decodedName, code);
	}

	public byte[] inject(final byte[] code, final String className, final String methodName, final String methodDesc, final int opcodeToInjectAfter, final InjectHelper helper) {
		final ClassNode cn = new ClassNode(262144);
		final ClassReader cr = new ClassReader(code);
		cr.accept((ClassVisitor) cn, 262144);
		for (final MethodNode method : cn.methods) {
			if (method.name.equals(methodName)) {
				if (!method.desc.equals(methodDesc))
					continue;
				AbstractInsnNode instruction;
				for (instruction = method.instructions.getFirst(); instruction != null && instruction.getOpcode() != opcodeToInjectAfter; instruction = instruction.getNext());
				final MethodNode methodNode = method;
				methodNode.maxStack += 10;
				final MethodNode methodNode2 = method;
				methodNode2.maxLocals += 10;
				if (instruction == null)
					break;
				final InsnList list = new InsnList();
				helper.inject(list);
				method.instructions.insert(instruction, list);
				final ClassWriter cw = new ClassWriter(262144);
				cn.accept((ClassVisitor) cw);
				return cw.toByteArray();
			}
		}
		System.out.println("Failed to inject code into " + className + ". Agglomerator won't work.");
		return code;
	}

	public byte[] injectSimple(final byte[] code, final String className, final String methodName, final String methodDesc, final int opcodeToInjectAfter, final String classToCall, final String methodToCall, final String descToCall) {
		final ClassNode cn = new ClassNode(262144);
		final ClassReader cr = new ClassReader(code);
		cr.accept((ClassVisitor) cn, 262144);
		for (final MethodNode method : cn.methods) {
			if (method.name.equals(methodName)) {
				if (!method.desc.equals(methodDesc))
					continue;
				AbstractInsnNode instruction;
				for (instruction = method.instructions.getFirst(); instruction != null && instruction.getOpcode() != opcodeToInjectAfter; instruction = instruction.getNext());
				if (instruction == null)
					break;
				final InsnList list = new InsnList();
				list.add((AbstractInsnNode) new VarInsnNode(21, 1));
				list.add((AbstractInsnNode) new MethodInsnNode(184, classToCall, methodToCall, descToCall));
				list.add((AbstractInsnNode) new VarInsnNode(54, 1));
				method.instructions.insert(instruction, list);
				final ClassWriter cw = new ClassWriter(262144);
				cn.accept((ClassVisitor) cw);
				return cw.toByteArray();
			}
		}
		System.out.println("Failed to inject code into " + className + ". Agglomerator won't work.");
		return code;
	}

	abstract class InjectHelper {
		abstract void inject(final InsnList p0);
	}
}