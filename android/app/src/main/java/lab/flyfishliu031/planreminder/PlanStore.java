package lab.flyfishliu031.planreminder;

import android.util.AtomicFile;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.*;
import org.json.*;

final class PlanStore {
  static final int LIMIT = 20 * 1024 * 1024;
  private final AtomicFile file;
  private final File previous;
  private List<Plan> plans = new ArrayList<>();

  PlanStore(File directory) {
    directory.mkdirs();
    file = new AtomicFile(new File(directory, "plans.json"));
    previous = new File(directory, "plans.previous.json");
  }

  synchronized void load() throws Exception {
    if (file.getBaseFile().exists() || new File(file.getBaseFile() + ".bak").exists())
      try (InputStream in = file.openRead()) {
        plans = read(in);
      }
  }

  synchronized boolean hasPrevious() {
    return previous.exists();
  }

  synchronized void restore() throws Exception {
    List<Plan> restored;
    try (InputStream in = new FileInputStream(previous)) {
      restored = read(in);
    }
    // Preserve a damaged file for recovery; never overwrite the known good backup.
    if (file.getBaseFile().exists())
      Files.copy(
          file.getBaseFile().toPath(),
          new File(file.getBaseFile() + ".corrupt-" + UUID.randomUUID()).toPath());
    write(restored, false);
  }

  synchronized List<Plan> all() {
    return new ArrayList<>(plans);
  }

  synchronized void save(Plan p) throws Exception {
    p.validate();
    List<Plan> updated = all();
    updated.removeIf(x -> x.id.equals(p.id));
    updated.add(p);
    write(updated, true);
  }

  synchronized void delete(String id) throws Exception {
    List<Plan> updated = all();
    updated.removeIf(x -> x.id.equals(id));
    write(updated, true);
  }

  synchronized byte[] export() throws Exception {
    return encode(plans);
  }

  synchronized int merge(InputStream in) throws Exception {
    List<Plan> incoming = read(in);
    Map<String, Plan> merged = new LinkedHashMap<>();
    for (Plan p : plans) merged.put(p.id, p);
    int count = 0;
    for (Plan p : incoming) {
      Plan old = merged.get(p.id);
      if (old == null || p.updatedAt.isAfter(old.updatedAt)) {
        merged.put(p.id, p);
        count++;
      }
    }
    if (count > 0) write(new ArrayList<>(merged.values()), true);
    return count;
  }

  static List<Plan> read(InputStream in) throws Exception {
    ByteArrayOutputStream data = new ByteArrayOutputStream();
    byte[] buffer = new byte[8192];
    int n;
    while ((n = in.read(buffer)) != -1) {
      if (data.size() + n > LIMIT) Plan.fail("备份超过 20 MB。");
      data.write(buffer, 0, n);
    }
    String text = data.toString(StandardCharsets.UTF_8.name());
    if (text.startsWith("\uFEFF")) text = text.substring(1);
    JSONObject doc = new JSONObject(text);
    Iterator<String> keys = doc.keys();
    while (keys.hasNext())
      if (!Arrays.asList("schemaVersion", "plans").contains(keys.next())) Plan.fail("备份包含未知字段。");
    Object schema = doc.get("schemaVersion");
    if (!(schema instanceof Number)
        || ((Number) schema).doubleValue() != ((Number) schema).intValue()) Plan.fail("备份版本无效。");
    int version = ((Number) schema).intValue();
    if (version != 1 && version != 2 && version != 3) Plan.fail("不支持此备份版本。");
    JSONArray a = doc.getJSONArray("plans");
    Set<String> ids = new HashSet<>();
    List<Plan> result = new ArrayList<>();
    for (int i = 0; i < a.length(); i++) {
      Plan p = Plan.from(a.getJSONObject(i));
      if (!ids.add(p.id)) Plan.fail("备份存在重复编号。");
      result.add(p);
    }
    return result;
  }

  private static byte[] encode(List<Plan> items) throws Exception {
    JSONArray a = new JSONArray();
    for (Plan p : items) {
      p.validate();
      a.put(p.json());
    }
    byte[] data =
        new JSONObject()
            .put("schemaVersion", 3)
            .put("plans", a)
            .toString(2)
            .getBytes(StandardCharsets.UTF_8);
    if (data.length > LIMIT) Plan.fail("计划数据超过 20 MB，请先导出并清理旧计划。");
    return data;
  }

  private void write(List<Plan> updated, boolean backup) throws Exception {
    byte[] data = encode(updated);
    if (backup && file.getBaseFile().exists()) {
      AtomicFile copy = new AtomicFile(previous);
      FileOutputStream out = null;
      try {
        out = copy.startWrite();
        out.write(encode(plans));
        copy.finishWrite(out);
      } catch (Exception e) {
        copy.failWrite(out);
        throw e;
      }
    }
    FileOutputStream out = null;
    try {
      out = file.startWrite();
      out.write(data);
      file.finishWrite(out);
    } catch (Exception e) {
      file.failWrite(out);
      throw e;
    }
    plans = new ArrayList<>(updated);
  }
}
