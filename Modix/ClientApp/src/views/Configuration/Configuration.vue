<template>
    <div class="configuration">
        <section class="section">
            <div class="container">
                <div class="columns">

                    <div class="column is-one-fifth">
                        <aside class="menu">
                            <div>
                                <p class="menu-label">
                                    Configuration
                                </p>
                                <ul class="menu-list">

                                    <li v-for="route in routes" :key="route.path">
                                        <router-link
                                            :class="{'is-disabled': !hasClaimsForRoute(route)}" v-tooltip.right-end="claimsFor(route)"
                                            :key="route.routeData.name" :to="{'name': route.routeData.name}" exact-active-class="is-active"
                                            :event="!hasClaimsForRoute(route) ? '' : 'click'">

                                            {{toTitleCase(route.title)}}
                                        </router-link>
                                    </li>

                                </ul>
                            </div>
                        </aside>
                    </div>

                    <div class="column is-four-fifths">
                        <router-view />
                    </div>

                </div>
            </div>
        </section>
    </div>
</template>

<script lang="ts">
import { Component, Prop, Vue, Watch } from 'vue-property-decorator';
import ModixRoute from '@/app/ModixRoute';
import {toTitleCase} from '@/app/Util';
import store from "@/app/Store";
import * as _ from 'lodash';

@Component({
    components:
    {

    },
})
export default class Configuration extends Vue
{
    created()
    {

    }

    get routes(): ModixRoute[]
    {
        let allRoutes = (<any>this.$router).options.routes;
        let currentChildren = _.find(allRoutes, route => route.name == 'config').children;

        return <any>_.map(currentChildren, child => child.meta as ModixRoute);
    }

    hasClaimsForRoute(route: ModixRoute): boolean
    {
        return store.userHasClaims(route.requiredClaims || []);
    }

    toTitleCase(input: string)
    {
        return toTitleCase(input);
    }

    claimsFor(route: ModixRoute)
    {
        if (this.hasClaimsForRoute(route))
        {
            return "";
        }

        if (!route.requiredClaims)
        {
            return "Disallowed";
        }

        return "Required Claims: " + route.requiredClaims.toString();
    }
}
</script>
