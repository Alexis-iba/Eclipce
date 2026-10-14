import bpy, math, os
from mathutils import Vector

PROJECT = 'C:/Users/ibarr/EclipseGenesis'
OUT = PROJECT + '/Assets/_EclipseGenesis/Art/Imported/Weapons/ReferenceWeapons'
os.makedirs(OUT, exist_ok=True)
scene = bpy.data.scenes.new('EG_ReferenceWeaponWorkshop')
bpy.context.window.scene = scene
mats = {}
for name, color, metal in [('EG_GunMetal',(.12,.13,.15),.7), ('EG_GunPolymer',(.025,.026,.03),0), ('EG_GunSteel',(.38,.4,.43),.85), ('EG_GunAccent',(.3,.035,.02),.45), ('EG_SandPolymer',(.32,.23,.13),0), ('EG_PlasmaArmor',(.12,.035,.24),.65), ('EG_PlasmaCore',(.16,.02,1),.25)]:
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal
    p.inputs['Roughness'].default_value=.32 if metal else .55
    mats[name]=m

def cube(name,pos,size,mat='EG_GunMetal',rot=0,bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    o=bpy.context.object; o.name=name; o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.rotation_euler.x=rot
    o.data.materials.append(mats[mat])
    if bevel:
        mod=o.modifiers.new('Machined edge','BEVEL');mod.width=bevel;mod.segments=3
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');mod.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def cyl(name,pos,radius,length,mat='EG_GunSteel',axis='Y',vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=length,location=pos)
    o=bpy.context.object;o.name=name;o.data.materials.append(mats[mat])
    if axis=='Y':o.rotation_euler.x=math.pi/2
    if axis=='X':o.rotation_euler.y=math.pi/2
    for p in o.data.polygons:p.use_smooth=len(p.vertices)==4
    return o

def rail(y0,y1,z=.08):
    cube('Picatinny base',(0,(y0+y1)/2,z),(.044,y1-y0,.008))
    for i in range(int((y1-y0)/.018)):
        cube('Rail tooth',(0,y0+i*.018,z+.008),(.053,.009,.009),bevel=.001)

def grip(y=-.08,sand=False):
    mat='EG_SandPolymer' if sand else 'EG_GunPolymer'
    cube('PistolGrip',(0,y-.025,-.105),(.037,.06,.145),mat,-.3,.009)
    for x in [-.02,.02]:
        for z in [-.06,-.09,-.12,-.15]:cube('Grip stipple',(x,y-.02,z),(.002,.043,.006),mat,bevel=.001)
    cube('Trigger guard',(0,y+.04,-.09),(.012,.085,.008),'EG_GunSteel')
    cube('Trigger front',(0,y+.078,-.065),(.008,.008,.05),'EG_GunSteel')
    cube('Trigger',(0,y+.024,-.066),(.006,.012,.034),'EG_GunSteel',-.3)

def stock(y=-.18,sand=False):
    mat='EG_SandPolymer' if sand else 'EG_GunPolymer'
    cyl('Buffer tube',(0,y-.065,.01),.016,.16)
    cube('Stock spine',(0,y-.16,.027),(.04,.19,.044),mat)
    cube('Stock butt',(0,y-.24,-.035),(.045,.025,.14),mat,bevel=.008)
    cube('Stock lower brace',(0,y-.16,-.05),(.017,.17,.022),mat,-.25)
    for i in range(6):cube('Shoulder pad',(0,y-.258,-.09+i*.02),(.046,.004,.012),'EG_GunPolymer',bevel=.001)

def magazine(y=.08,length=.17,curved=True):
    o=cube('Magazine',(0,y,-length*.5-.03),(.032,.059,length),'EG_GunPolymer',.12 if curved else 0,.007)
    for x in [-.017,.017]:
        for i in range(3):cube('Magazine rib',(x,y-.019+i*.02,-length*.5-.03),(.003,.009,length*.82),'EG_GunMetal',.12 if curved else 0, .001)
    return o

def screws(y0,y1):
    for y in [y0,y0+.05,y1-.05,y1]:
        for x in [-.038,.038]:cyl('Fastener',(x,y,.014),.006,.004,'EG_GunSteel','X',12)
    cube('Ejection port',(.039,.035,.025),(.004,.085,.029),'EG_GunPolymer',bevel=.001)
    cyl('Charging handle',(.054,-.07,.038),.009,.035,'EG_GunSteel','X',16)
    cube('Selector switch',(-.041,-.07,-.024),(.005,.029,.01),'EG_GunAccent',bevel=.001)

def holo(y=-.035):
    cube('Optic mount',(0,y,.104),(.05,.07,.024))
    for x in [-.026,.026]:cube('Holo hood',(x,y,.15),(.007,.052,.075),'EG_GunPolymer')
    cube('Holo roof',(0,y,.188),(.058,.052,.008),'EG_GunPolymer')
    cube('Holo base',(0,y,.115),(.058,.052,.008))
    return (0,y,.15)

def socket(name,pos):
    o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);o.location=pos;return o

def build(kind):
    before=set(scene.objects)
    root=bpy.data.objects.new('SM_'+kind,None);scene.collection.objects.link(root)
    if kind=='VX9':
        cube('UpperReceiver',(0,.045,.015),(.07,.28,.078))
        cube('LowerReceiver',(0,-.015,-.034),(.062,.16,.038),'EG_GunPolymer')
        cube('ShortHandguard',(0,.20,.017),(.066,.15,.07),'EG_GunPolymer')
        for x in [-.035,.035]:
            for y in [.16,.19,.22,.25]:cube('Mlok recess',(x,y,.01),(.004,.019,.017),'EG_GunSteel',bevel=.002)
        cyl('Barrel',(0,.31,.025),.011,.17)
        cyl('Compensator',(0,.398,.025),.021,.048,'EG_GunMetal')
        grip();magazine(.025,.19,False);stock(-.12);rail(-.10,.27);screws(-.09,.12);sight=holo()
        muzzle=(0,.424,.025)
    elif kind=='BR12':
        cube('Shotgun receiver',(0,.015,.008),(.077,.25,.087))
        cyl('Long barrel',(0,.415,.035),.018,.56)
        cyl('Shell tube',(0,.36,-.012),.016,.48)
        cube('Pump',(0,.265,-.009),(.067,.18,.069),'EG_SandPolymer',bevel=.012)
        for y in [.20,.225,.25,.275,.30,.325]:cube('Pump groove',(0,y,-.015),(.072,.006,.055),'EG_GunPolymer',bevel=.003)
        grip(sand=True);stock(-.15,True);rail(-.10,.125);screws(-.08,.11);sight=holo(-.04)
        cube('Shell saddle',(-.045,-.01,.003),(.012,.13,.024),'EG_GunPolymer')
        for y in [-.055,-.025,.005,.035]:cyl('Shell holder',(-.055,y,.001),.01,.07,'EG_GunAccent','Z',16)
        muzzle=(0,.70,.035)
    elif kind=='NS50':
        cube('Precision receiver',(0,.055,.022),(.071,.32,.073))
        cube('Chassis',(0,.13,-.035),(.059,.54,.041),'EG_SandPolymer')
        cube('Long forend',(0,.37,.003),(.066,.31,.063),'EG_SandPolymer')
        for x in [-.035,.035]:
            for y in [.25,.29,.33,.37,.41,.45]:cube('Forend slot',(x,y,.005),(.004,.024,.019),'EG_GunPolymer')
        cyl('Precision barrel',(0,.66,.023),.014,.54)
        cyl('Muzzle brake',(0,.93,.023),.024,.085,'EG_GunMetal')
        for x in [-.026,.026]:
            for y in [.906,.929,.952]:cube('Brake vent',(x,y,.023),(.003,.014,.018),'EG_GunPolymer',bevel=.001)
        grip(sand=True);stock(-.16,True);magazine(.09,.095,False);rail(-.12,.21);screws(-.09,.15)
        cyl('Scope tube',(0,.026,.165),.026,.285,'EG_GunMetal')
        cyl('Scope objective',(0,.197,.165),.04,.065,'EG_GunPolymer')
        cyl('Scope ocular',(0,-.138,.165),.032,.065,'EG_GunPolymer')
        for y in [-.045,.1]:
            cube('Scope mount',(0,y,.113),(.045,.035,.065))
            cyl('Scope ring',(0,y,.165),.030,.023)
        cyl('Elevation turret',(0,.03,.203),.018,.03,'EG_GunMetal','Z')
        cyl('Windage turret',(.033,.03,.165),.017,.027,'EG_GunMetal','X')
        cyl('Bolt handle',(.075,-.08,.015),.008,.07,'EG_GunSteel','X')
        for x in [-.029,.029]:cube('Folded bipod',(x,.52,-.046),(.012,.19,.018),'EG_GunSteel',bevel=.003)
        sight=(0,-.18,.165);muzzle=(0,.975,.023)
    else:
        cube('Plasma receiver',(0,.08,.02),(.11,.40,.10),'EG_PlasmaArmor',bevel=.018)
        cyl('Ion chamber',(0,.15,.02),.045,.25,'EG_PlasmaCore')
        for x in [-.062,.062]:
            for y in [-.06,.02,.10,.18,.26]:cube('Armored vanes',(x,y,.028),(.025,.054,.085),'EG_PlasmaArmor',-.15,.008)
        for x in [-.052,.052]:
            cube('Forward prong',(x,.405,.035),(.027,.29,.046),'EG_PlasmaArmor',bevel=.01)
            cube('Emitter filament',(x,.40,.065),(.008,.23,.007),'EG_PlasmaCore',bevel=.002)
        cube('PowerCell',(0,.06,-.103),(.074,.09,.15),'EG_PlasmaArmor',.12,.017)
        for x in [-.038,.038]:cube('Cell charge strip',(x,.06,-.10),(.006,.064,.105),'EG_PlasmaCore',bevel=.002)
        grip();stock(-.16);rail(-.09,.10);sight=holo(-.02);muzzle=(0,.55,.035)
    socket('Socket_Muzzle',muzzle);socket('Socket_Sight',sight);socket('Socket_ShellEject',(.05,.025,.025))
    objects=[o for o in scene.objects if o not in before and o!=root]
    for o in objects:o.parent=root
    # Keep distinct moving parts; all geometry has explicit physical dimensions and bevels.
    bpy.ops.object.select_all(action='DESELECT')
    for o in [root]+objects:o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=OUT+'/SM_'+kind+'.fbx',use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
    return {'name':kind,'parts':len(objects),'vertices':sum(len(o.data.vertices) for o in objects if o.type=='MESH')}

result=[build(k) for k in ['VX9','BR12','NS50','PLASMAX']]
os.makedirs(PROJECT+'/Docs/SourceAssets',exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=PROJECT+'/Docs/SourceAssets/Eclipse_ReferenceWeapons.blend')
print(result)
