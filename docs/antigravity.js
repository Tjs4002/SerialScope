/*
 * Antigravity: particles that gather into a wavy ring around the pointer.
 * Ported to plain three.js from the <Antigravity /> component by React Bits
 * (https://reactbits.dev). Usage: Antigravity(element, { color: '#22c55e', ... })
 */
import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.min.js';

const DEFAULTS = {
  count: 300,
  magnetRadius: 10,
  ringRadius: 10,
  waveSpeed: 0.4,
  waveAmplitude: 1,
  particleSize: 2,
  lerpSpeed: 0.1,
  color: '#FF9FFC',
  opacity: 1,
  autoAnimate: false,
  particleVariance: 1,
  rotationSpeed: 0,
  depthFactor: 1,
  pulseSpeed: 3,
  particleShape: 'capsule',
  fieldStrength: 10,
  pointerTarget: null
};

const geometryFor = shape => {
  if (shape === 'sphere') return new THREE.SphereGeometry(0.2, 16, 16);
  if (shape === 'box') return new THREE.BoxGeometry(0.3, 0.3, 0.3);
  if (shape === 'tetrahedron') return new THREE.TetrahedronGeometry(0.3);
  return new THREE.CapsuleGeometry(0.1, 0.4, 4, 8);
};

export default function Antigravity(container, options) {
  const o = { ...DEFAULTS, ...options };

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.domElement.style.display = 'block';
  renderer.domElement.style.width = '100%';
  renderer.domElement.style.height = '100%';
  container.appendChild(renderer.domElement);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(35, 1, 0.1, 1000);
  camera.position.set(0, 0, 50);

  const material = new THREE.MeshBasicMaterial({ color: o.color, transparent: o.opacity < 1, opacity: o.opacity });
  const mesh = new THREE.InstancedMesh(geometryFor(o.particleShape), material, o.count);
  scene.add(mesh);

  const dummy = new THREE.Object3D();
  const clock = new THREE.Clock();
  const viewport = { width: 1, height: 1 };
  const pointer = { x: 0, y: 0 };
  const lastMousePos = { x: 0, y: 0 };
  const virtualMouse = { x: 0, y: 0 };
  let lastMouseMoveTime = 0;
  let particles = [];

  const makeParticles = () => {
    particles = [];
    for (let i = 0; i < o.count; i++) {
      const x = (Math.random() - 0.5) * viewport.width;
      const y = (Math.random() - 0.5) * viewport.height;
      const z = (Math.random() - 0.5) * 20;
      particles.push({
        t: Math.random() * 100,
        speed: 0.01 + Math.random() / 200,
        mx: x, my: y, mz: z,
        cx: x, cy: y, cz: z,
        randomRadiusOffset: (Math.random() - 0.5) * 2
      });
    }
  };

  const resize = () => {
    const w = Math.max(1, container.clientWidth);
    const h = Math.max(1, container.clientHeight);
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    viewport.height = 2 * Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)) * camera.position.z;
    viewport.width = viewport.height * camera.aspect;
    makeParticles();
  };

  const onMove = e => {
    const rect = container.getBoundingClientRect();
    pointer.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
    pointer.y = -(((e.clientY - rect.top) / rect.height) * 2 - 1);
  };
  (o.pointerTarget || container).addEventListener('pointermove', onMove, { passive: true });

  const frame = () => {
    const m = pointer;
    const v = viewport;
    const mouseDist = Math.hypot(m.x - lastMousePos.x, m.y - lastMousePos.y);
    if (mouseDist > 0.001) {
      lastMouseMoveTime = Date.now();
      lastMousePos.x = m.x;
      lastMousePos.y = m.y;
    }

    let destX = (m.x * v.width) / 2;
    let destY = (m.y * v.height) / 2;
    const time = clock.getElapsedTime();
    if (o.autoAnimate && Date.now() - lastMouseMoveTime > 2000) {
      destX = Math.sin(time * 0.5) * (v.width / 4);
      destY = Math.cos(time * 0.5 * 2) * (v.height / 4);
    }

    virtualMouse.x += (destX - virtualMouse.x) * 0.05;
    virtualMouse.y += (destY - virtualMouse.y) * 0.05;
    const targetX = virtualMouse.x;
    const targetY = virtualMouse.y;
    const globalRotation = time * o.rotationSpeed;

    particles.forEach((p, i) => {
      const t = (p.t += p.speed / 2);
      const projectionFactor = 1 - p.cz / 50;
      const ptx = targetX * projectionFactor;
      const pty = targetY * projectionFactor;
      const dx = p.mx - ptx;
      const dy = p.my - pty;
      const dist = Math.sqrt(dx * dx + dy * dy);

      let tx = p.mx, ty = p.my, tz = p.mz * o.depthFactor;
      if (dist < o.magnetRadius) {
        const angle = Math.atan2(dy, dx) + globalRotation;
        const wave = Math.sin(t * o.waveSpeed + angle) * (0.5 * o.waveAmplitude);
        const deviation = p.randomRadiusOffset * (5 / (o.fieldStrength + 0.1));
        const r = o.ringRadius + wave + deviation;
        tx = ptx + r * Math.cos(angle);
        ty = pty + r * Math.sin(angle);
        tz = p.mz * o.depthFactor + Math.sin(t) * (o.waveAmplitude * o.depthFactor);
      }

      p.cx += (tx - p.cx) * o.lerpSpeed;
      p.cy += (ty - p.cy) * o.lerpSpeed;
      p.cz += (tz - p.cz) * o.lerpSpeed;

      dummy.position.set(p.cx, p.cy, p.cz);
      dummy.lookAt(ptx, pty, p.cz);
      dummy.rotateX(Math.PI / 2);

      const distFromRing = Math.abs(Math.hypot(p.cx - ptx, p.cy - pty) - o.ringRadius);
      const scaleFactor = Math.max(0, Math.min(1, 1 - distFromRing / 10));
      const s = scaleFactor * (0.8 + Math.sin(t * o.pulseSpeed) * 0.2 * o.particleVariance) * o.particleSize;
      dummy.scale.set(s, s, s);
      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);
    });
    mesh.instanceMatrix.needsUpdate = true;
    renderer.render(scene, camera);
  };

  let visible = true;
  new IntersectionObserver(entries => {
    visible = entries[0].isIntersecting;
    renderer.setAnimationLoop(visible && !document.hidden ? frame : null);
  }).observe(container);
  document.addEventListener('visibilitychange', () => {
    renderer.setAnimationLoop(visible && !document.hidden ? frame : null);
  });

  new ResizeObserver(resize).observe(container);
  resize();
  renderer.setAnimationLoop(frame);
}
